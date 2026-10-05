using Microsoft.EntityFrameworkCore;
using Npgsql;
using OshikatsuApi.Data;
using OshikatsuApi.DTOs;
using OshikatsuApi.Entities;
using OshikatsuApi.Exceptions;

namespace OshikatsuApi.Services;

/// <inheritdoc />
public class InternalCollectorService : IInternalCollectorService
{
    private readonly AppDbContext _db;

    public InternalCollectorService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<InternalNewsResponseDto> CreateNewsItemAsync(CreateNewsItemRequestDto dto)
    {
        // タレントの存在確認
        var talentExists = await _db.Talents.AnyAsync(t => t.Id == dto.TalentId);
        if (!talentExists)
            throw new TalentNotFoundException();

        // 同一ソースURLが同一タレントで既に存在する場合は無視（冪等）
        var existing = await _db.NewsItems
            .FirstOrDefaultAsync(n => n.SourceUrl == dto.SourceUrl && n.TalentId == dto.TalentId);

        if (existing is not null)
        {
            return ToNewsDto(existing, wasDuplicate: true);
        }

        // 公開日時が未取得の場合は収集日時で代替し、推定フラグを立てる
        // Npgsql は UTC 以外のオフセット付き DateTimeOffset を timestamptz に書き込めないため
        // 受信した DateTimeOffset をすべて UTC に正規化してから保存する
        var isEstimated = dto.PublishedAt is null;
        var publishedAt = (dto.PublishedAt ?? dto.CollectedAt).ToUniversalTime();
        var collectedAt = dto.CollectedAt.ToUniversalTime();

        var now = DateTimeOffset.UtcNow;
        var item = new NewsItem
        {
            TalentId = dto.TalentId,
            Title = dto.Title,
            Summary = dto.Summary,
            SourceUrl = dto.SourceUrl,
            PublishedAt = publishedAt,
            IsPublishedAtEstimated = isEstimated,
            CollectedAt = collectedAt,
            SourceName = string.IsNullOrWhiteSpace(dto.SourceName) ? null : dto.SourceName.Trim(),
            CreatedAt = now,
        };

        _db.NewsItems.Add(item);
        await _db.SaveChangesAsync();

        return ToNewsDto(item, wasDuplicate: false);
    }

    /// <inheritdoc />
    public async Task<InternalProgramResponseDto> CreateProgramAppearanceAsync(CreateProgramAppearanceRequestDto dto)
    {
        // タレントの存在確認
        var talentExists = await _db.Talents.AnyAsync(t => t.Id == dto.TalentId);
        if (!talentExists)
            throw new TalentNotFoundException();

        // 同一ソースURLが同一タレントで既に番組として存在する場合は無視（冪等）
        var existing = await _db.ProgramAppearances
            .FirstOrDefaultAsync(p => p.SourceUrl == dto.SourceUrl && p.TalentId == dto.TalentId);

        if (existing is not null)
        {
            return ToProgramDto(existing, wasDuplicate: true);
        }

        // FEAT-024: 同タレントのニュースに同一 sourceUrl が存在する場合もスキップ
        var existsInNews = await _db.NewsItems
            .AnyAsync(n => n.SourceUrl == dto.SourceUrl && n.TalentId == dto.TalentId);

        if (existsInNews)
        {
            // 番組レコードは作らずスキップを示す応答を返す（wasDuplicate: true）
            return new InternalProgramResponseDto
            {
                Id = 0,
                TalentId = dto.TalentId,
                ProgramName = dto.ProgramName,
                Broadcaster = dto.Broadcaster,
                AiredAt = dto.AiredAt?.ToUniversalTime(),
                IsAiredAtEstimated = dto.AiredAt is null,
                SourceUrl = dto.SourceUrl,
                CollectedAt = dto.CollectedAt.ToUniversalTime(),
                CreatedAt = DateTimeOffset.UtcNow,
                WasDuplicate = true,
            };
        }

        // 放送日時が未取得の場合は収集日時で代替し、推定フラグを立てる
        var isEstimated = dto.AiredAt is null;
        var airedAt = (dto.AiredAt ?? dto.CollectedAt).ToUniversalTime();
        var collectedAt = dto.CollectedAt.ToUniversalTime();

        var now = DateTimeOffset.UtcNow;
        var item = new ProgramAppearance
        {
            TalentId = dto.TalentId,
            ProgramName = dto.ProgramName,
            Broadcaster = dto.Broadcaster,
            AiredAt = airedAt,
            IsAiredAtEstimated = isEstimated,
            SourceUrl = dto.SourceUrl,
            CollectedAt = collectedAt,
            SourceName = string.IsNullOrWhiteSpace(dto.SourceName) ? null : dto.SourceName.Trim(),
            CreatedAt = now,
        };

        _db.ProgramAppearances.Add(item);
        await _db.SaveChangesAsync();

        return ToProgramDto(item, wasDuplicate: false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InternalOshiDto>> GetAllOshisAsync()
    {
        // FEAT-040: 1人以上のユーザーが登録しているタレントのみ返す
        return await _db.Talents
            .Where(t => t.UserOshis.Any())
            .OrderBy(t => t.Id)
            .Select(t => new InternalOshiDto { TalentId = t.Id, Name = t.DisplayName })
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<BatchResultResponseDto> BatchCreateNewsItemsAsync(BatchCreateNewsRequestDto dto)
    {
        // タレントの存在確認
        var talentExists = await _db.Talents.AnyAsync(t => t.Id == dto.TalentId);
        if (!talentExists)
            throw new TalentNotFoundException();

        // 当該タレントに紐づく既存 sourceUrl を一括取得（DB の一括照合）
        var existingUrlsList = await _db.NewsItems
            .Where(n => n.TalentId == dto.TalentId)
            .Select(n => n.SourceUrl)
            .ToListAsync();
        var existingUrls = new HashSet<string>(existingUrlsList, StringComparer.Ordinal);

        // リクエスト内の重複を排除（同一 sourceUrl の初回出現のみ採用）
        var seenInRequest = new HashSet<string>(StringComparer.Ordinal);
        var newItems = new List<NewsItem>();
        var skippedCount = 0;
        var now = DateTimeOffset.UtcNow;

        foreach (var item in dto.Items)
        {
            // FEAT-030: 画像箱タイトルはスキップ
            if (GalleryTitleDetector.IsGalleryTitle(item.Title))
            {
                skippedCount++;
                continue;
            }

            if (!seenInRequest.Add(item.SourceUrl) || existingUrls.Contains(item.SourceUrl))
            {
                skippedCount++;
                continue;
            }

            var isEstimated = item.PublishedAt is null;
            var publishedAt = (item.PublishedAt ?? item.CollectedAt).ToUniversalTime();
            var collectedAt = item.CollectedAt.ToUniversalTime();

            newItems.Add(new NewsItem
            {
                TalentId = dto.TalentId,
                Title = item.Title,
                Summary = item.Summary,
                SourceUrl = item.SourceUrl,
                PublishedAt = publishedAt,
                IsPublishedAtEstimated = isEstimated,
                CollectedAt = collectedAt,
                SourceName = string.IsNullOrWhiteSpace(item.SourceName) ? null : item.SourceName.Trim(),
                CreatedAt = now,
            });
        }

        if (newItems.Count > 0)
        {
            _db.NewsItems.AddRange(newItems);
            // ユニーク制約違反（23505）のみスキップ扱いとし、それ以外の DB エラーは再スロー
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                skippedCount += newItems.Count;
                newItems.Clear();
            }
        }

        return new BatchResultResponseDto
        {
            ReceivedCount = dto.Items.Count,
            SavedCount = newItems.Count,
            SkippedCount = skippedCount,
        };
    }

    /// <inheritdoc />
    public async Task<BatchResultResponseDto> BatchCreateProgramAppearancesAsync(BatchCreateProgramsRequestDto dto)
    {
        // タレントの存在確認
        var talentExists = await _db.Talents.AnyAsync(t => t.Id == dto.TalentId);
        if (!talentExists)
            throw new TalentNotFoundException();

        // 当該タレントに紐づく既存の番組 sourceUrl を一括取得
        var existingProgramUrlsList = await _db.ProgramAppearances
            .Where(p => p.TalentId == dto.TalentId)
            .Select(p => p.SourceUrl)
            .ToListAsync();
        var existingProgramUrls = new HashSet<string>(existingProgramUrlsList, StringComparer.Ordinal);

        // FEAT-024: 同タレントのニュースに既存の sourceUrl も除外対象
        var existingNewsUrlsList = await _db.NewsItems
            .Where(n => n.TalentId == dto.TalentId)
            .Select(n => n.SourceUrl)
            .ToListAsync();
        var existingNewsUrls = new HashSet<string>(existingNewsUrlsList, StringComparer.Ordinal);

        // リクエスト内の重複を排除（同一 sourceUrl の初回出現のみ採用）
        var seenInRequest = new HashSet<string>(StringComparer.Ordinal);
        var newItems = new List<ProgramAppearance>();
        var skippedCount = 0;
        var now = DateTimeOffset.UtcNow;

        foreach (var item in dto.Items)
        {
            // FEAT-030: 画像箱タイトルはスキップ
            if (GalleryTitleDetector.IsGalleryTitle(item.ProgramName))
            {
                skippedCount++;
                continue;
            }

            if (!seenInRequest.Add(item.SourceUrl)
                || existingProgramUrls.Contains(item.SourceUrl)
                || existingNewsUrls.Contains(item.SourceUrl))
            {
                skippedCount++;
                continue;
            }

            var isEstimated = item.AiredAt is null;
            var airedAt = (item.AiredAt ?? item.CollectedAt).ToUniversalTime();
            var collectedAt = item.CollectedAt.ToUniversalTime();

            newItems.Add(new ProgramAppearance
            {
                TalentId = dto.TalentId,
                ProgramName = item.ProgramName,
                Broadcaster = item.Broadcaster,
                AiredAt = airedAt,
                IsAiredAtEstimated = isEstimated,
                SourceUrl = item.SourceUrl,
                CollectedAt = collectedAt,
                SourceName = string.IsNullOrWhiteSpace(item.SourceName) ? null : item.SourceName.Trim(),
                CreatedAt = now,
            });
        }

        if (newItems.Count > 0)
        {
            _db.ProgramAppearances.AddRange(newItems);
            // ユニーク制約違反（23505）のみスキップ扱いとし、それ以外の DB エラーは再スロー
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                skippedCount += newItems.Count;
                newItems.Clear();
            }
        }

        return new BatchResultResponseDto
        {
            ReceivedCount = dto.Items.Count,
            SavedCount = newItems.Count,
            SkippedCount = skippedCount,
        };
    }

    /// <summary>
    /// DB 更新例外がユニーク制約違反（PostgreSQL エラーコード 23505）かどうかを判定する。
    /// true の場合のみ catch ブロックで捕捉し、それ以外の DB エラーは自然に再スローされる。
    /// </summary>
    protected virtual bool IsUniqueConstraintViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505";

    private static InternalNewsResponseDto ToNewsDto(NewsItem item, bool wasDuplicate) => new()
    {
        Id = item.Id,
        TalentId = item.TalentId ?? 0,
        Title = item.Title,
        SourceUrl = item.SourceUrl,
        PublishedAt = item.PublishedAt,
        IsPublishedAtEstimated = item.IsPublishedAtEstimated,
        CollectedAt = item.CollectedAt,
        CreatedAt = item.CreatedAt,
        WasDuplicate = wasDuplicate,
    };

    private static InternalProgramResponseDto ToProgramDto(ProgramAppearance item, bool wasDuplicate) => new()
    {
        Id = item.Id,
        TalentId = item.TalentId ?? 0,
        ProgramName = item.ProgramName,
        Broadcaster = item.Broadcaster,
        AiredAt = item.AiredAt,
        IsAiredAtEstimated = item.IsAiredAtEstimated,
        SourceUrl = item.SourceUrl,
        CollectedAt = item.CollectedAt,
        CreatedAt = item.CreatedAt,
        WasDuplicate = wasDuplicate,
    };
}
