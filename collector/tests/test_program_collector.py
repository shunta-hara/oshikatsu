"""collector.program_collector のテスト"""
from __future__ import annotations

from datetime import datetime, timezone
from unittest.mock import MagicMock

import pytest

from collector.client import BackendClient, BatchResult, OshiInfo, ProgramBatchItem
from collector.program_collector import (
    PROGRAM_KEYWORDS,
    _filter_program_entries,
    _is_program_related,
    run_program_collection,
)
from collector.program_fetcher import ProgramEntry, ProgramFetcher

_NOW = datetime(2026, 7, 14, 12, 0, 0, tzinfo=timezone.utc)

_OSHI_A = OshiInfo(id=1, name="推しA")
_OSHI_B = OshiInfo(id=2, name="推しB")

# キーワードを含むエントリ（フィルタ通過）
_ENTRY_PROGRAM = ProgramEntry(
    program_name="推しAの出演番組情報",
    source_url="https://tv.example.com/1",
    aired_at=_NOW,
    broadcaster=None,
)
_ENTRY_DRAMA = ProgramEntry(
    program_name="推しAのドラマ出演",
    source_url="https://tv.example.com/2",
    aired_at=_NOW,
    broadcaster=None,
)
# キーワードを含まないエントリ（フィルタで除外）
_ENTRY_NEWS_LIKE = ProgramEntry(
    program_name="推しAの最新情報まとめ",
    source_url="https://news.example.com/3",
    aired_at=_NOW,
    broadcaster=None,
)

_BATCH_RESULT_SAVED = BatchResult(received_count=1, saved_count=1, skipped_count=0)


def make_client(
    batch_result: BatchResult | None = None,
) -> BackendClient:
    """モック BackendClient を返す。"""
    client = MagicMock(spec=BackendClient)
    client.post_program_batch.return_value = batch_result or _BATCH_RESULT_SAVED
    client.post_job_log.return_value = {"id": 1, "jobType": "program"}
    return client


def make_fetcher(entries: list[ProgramEntry] | None = None) -> ProgramFetcher:
    """モック ProgramFetcher を返す。"""
    fetcher = MagicMock(spec=ProgramFetcher)
    fetcher.fetch_for_oshi.return_value = entries if entries is not None else [_ENTRY_PROGRAM]
    return fetcher


class TestIsProgramRelated:
    """_is_program_related のユニットテスト"""

    def test_title_with_keyword_returns_true(self) -> None:
        assert _is_program_related("推しAの出演情報") is True

    def test_title_with_tv_keyword_returns_true(self) -> None:
        assert _is_program_related("テレビ出演のお知らせ") is True

    def test_title_with_english_tv_returns_true(self) -> None:
        assert _is_program_related("TV番組に出演") is True

    def test_title_with_drama_returns_true(self) -> None:
        assert _is_program_related("ドラマ主演決定") is True

    def test_title_with_radio_returns_true(self) -> None:
        assert _is_program_related("ラジオに出演") is True

    def test_title_with_live_returns_true(self) -> None:
        assert _is_program_related("ライブ配信開始") is True

    def test_title_with_mc_returns_true(self) -> None:
        assert _is_program_related("MC担当決定") is True

    def test_title_with_guest_returns_true(self) -> None:
        assert _is_program_related("ゲスト出演") is True

    def test_title_without_keywords_returns_false(self) -> None:
        assert _is_program_related("最新情報まとめ") is False

    def test_empty_title_returns_false(self) -> None:
        assert _is_program_related("") is False

    def test_keywords_list_has_required_keywords(self) -> None:
        """PROGRAM_KEYWORDS に必須キーワードが含まれること"""
        required = ["出演", "番組", "放送", "テレビ", "TV", "ラジオ", "ドラマ", "映画", "配信", "ライブ", "MC", "ゲスト"]
        for kw in required:
            assert kw in PROGRAM_KEYWORDS


class TestFilterProgramEntries:
    """_filter_program_entries のユニットテスト"""

    def test_entry_with_keyword_passes_filter(self) -> None:
        result = _filter_program_entries([_ENTRY_PROGRAM], set())
        assert len(result) == 1
        assert result[0] == _ENTRY_PROGRAM

    def test_entry_without_keyword_is_excluded(self) -> None:
        result = _filter_program_entries([_ENTRY_NEWS_LIKE], set())
        assert len(result) == 0

    def test_entry_with_news_url_is_excluded(self) -> None:
        news_urls = {"https://tv.example.com/1"}
        result = _filter_program_entries([_ENTRY_PROGRAM], news_urls)
        assert len(result) == 0

    def test_mixed_entries_only_keyword_match_without_news_url_pass(self) -> None:
        news_urls = {"https://tv.example.com/1"}
        entries = [_ENTRY_PROGRAM, _ENTRY_DRAMA, _ENTRY_NEWS_LIKE]
        result = _filter_program_entries(entries, news_urls)
        # _ENTRY_PROGRAM: URL重複で除外
        # _ENTRY_DRAMA: キーワードあり・URL重複なし → 通過
        # _ENTRY_NEWS_LIKE: キーワードなし → 除外
        assert len(result) == 1
        assert result[0] == _ENTRY_DRAMA

    def test_empty_entries_returns_empty(self) -> None:
        result = _filter_program_entries([], set())
        assert result == []

    def test_empty_news_urls_no_url_exclusion(self) -> None:
        result = _filter_program_entries([_ENTRY_PROGRAM], set())
        assert len(result) == 1


class TestRunProgramCollection:
    # ── 正常系 ────────────────────────────────────────────────────────────────

    def test_run_collection_calls_fetch_for_each_oshi(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_PROGRAM])

        run_program_collection(client, fetcher, [_OSHI_A, _OSHI_B])

        assert fetcher.fetch_for_oshi.call_count == 2
        fetcher.fetch_for_oshi.assert_any_call("推しA")
        fetcher.fetch_for_oshi.assert_any_call("推しB")

    def test_run_collection_calls_batch_once_per_oshi(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_PROGRAM, _ENTRY_DRAMA])

        run_program_collection(client, fetcher, [_OSHI_A])

        client.post_program_batch.assert_called_once()

    def test_run_collection_passes_correct_oshi_id_to_batch(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_PROGRAM])

        run_program_collection(client, fetcher, [_OSHI_A])

        call_kwargs = client.post_program_batch.call_args.kwargs
        assert call_kwargs["oshi_id"] == 1

    def test_run_collection_batch_items_contain_program_name_and_url(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_PROGRAM])

        run_program_collection(client, fetcher, [_OSHI_A])

        call_kwargs = client.post_program_batch.call_args.kwargs
        items: list[ProgramBatchItem] = call_kwargs["items"]
        assert len(items) == 1
        assert items[0].program_name == "推しAの出演番組情報"
        assert items[0].source_url == "https://tv.example.com/1"

    # ── キーワードフィルタ（FEAT-024） ────────────────────────────────────────

    def test_entries_without_keywords_are_excluded(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_NEWS_LIKE])

        run_program_collection(client, fetcher, [_OSHI_A])

        # キーワードなしエントリは送信されない
        client.post_program_batch.assert_not_called()

    def test_entries_with_and_without_keywords_only_keyword_ones_sent(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_PROGRAM, _ENTRY_NEWS_LIKE])

        run_program_collection(client, fetcher, [_OSHI_A])

        client.post_program_batch.assert_called_once()
        call_kwargs = client.post_program_batch.call_args.kwargs
        items: list[ProgramBatchItem] = call_kwargs["items"]
        assert len(items) == 1
        assert items[0].program_name == "推しAの出演番組情報"

    # ── URL重複排除（FEAT-024） ───────────────────────────────────────────────

    def test_news_url_overlap_excludes_program_entry(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_PROGRAM])
        news_urls_by_oshi = {1: {"https://tv.example.com/1"}}

        run_program_collection(client, fetcher, [_OSHI_A], news_urls_by_oshi=news_urls_by_oshi)

        client.post_program_batch.assert_not_called()

    def test_news_urls_for_different_oshi_do_not_affect_current_oshi(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_PROGRAM])
        # 推しBのニュースURLが推しAには影響しない
        news_urls_by_oshi = {2: {"https://tv.example.com/1"}}

        run_program_collection(client, fetcher, [_OSHI_A], news_urls_by_oshi=news_urls_by_oshi)

        client.post_program_batch.assert_called_once()

    def test_no_news_urls_by_oshi_passes_all_keyword_matching_entries(self) -> None:
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_PROGRAM, _ENTRY_DRAMA])

        run_program_collection(client, fetcher, [_OSHI_A])

        call_kwargs = client.post_program_batch.call_args.kwargs
        assert len(call_kwargs["items"]) == 2

    # ── 境界値 ────────────────────────────────────────────────────────────────

    def test_run_collection_empty_oshi_list_skips_collection(self) -> None:
        client = make_client()
        fetcher = make_fetcher()

        run_program_collection(client, fetcher, [])

        fetcher.fetch_for_oshi.assert_not_called()
        client.post_program_batch.assert_not_called()

    def test_run_collection_empty_oshi_list_sends_job_log(self) -> None:
        client = make_client()
        fetcher = make_fetcher()

        run_program_collection(client, fetcher, [])

        client.post_job_log.assert_called_once()
        call_kwargs = client.post_job_log.call_args.kwargs
        assert call_kwargs["job_type"] == "program"
        assert call_kwargs["success_count"] == 0

    def test_run_collection_entry_without_aired_at_passes_none(self) -> None:
        entry_no_aired_at = ProgramEntry(
            program_name="番組の放送予定",
            source_url="https://tv.example.com/3",
            aired_at=None,
            broadcaster=None,
        )
        client = make_client()
        fetcher = make_fetcher(entries=[entry_no_aired_at])

        run_program_collection(client, fetcher, [_OSHI_A])

        call_kwargs = client.post_program_batch.call_args.kwargs
        items: list[ProgramBatchItem] = call_kwargs["items"]
        assert items[0].aired_at is None

    def test_run_collection_all_entries_filtered_out_no_batch_call(self) -> None:
        """フィルタ後に送信対象が0件になった場合は一括 API を呼ばない"""
        client = make_client()
        fetcher = make_fetcher(entries=[_ENTRY_NEWS_LIKE])

        run_program_collection(client, fetcher, [_OSHI_A])

        client.post_program_batch.assert_not_called()

    # ── 異常系 ────────────────────────────────────────────────────────────────

    def test_run_collection_fetch_error_continues_to_next_oshi(self) -> None:
        client = make_client()
        fetcher = MagicMock(spec=ProgramFetcher)
        fetcher.fetch_for_oshi.side_effect = [
            RuntimeError("RSS fetch failed"),
            [_ENTRY_PROGRAM],
        ]

        run_program_collection(client, fetcher, [_OSHI_A, _OSHI_B])

        client.post_program_batch.assert_called_once()

    def test_run_collection_fetch_error_counts_as_failed(self) -> None:
        client = make_client()
        fetcher = MagicMock(spec=ProgramFetcher)
        fetcher.fetch_for_oshi.side_effect = RuntimeError("RSS fetch failed")

        run_program_collection(client, fetcher, [_OSHI_A])

        call_kwargs = client.post_job_log.call_args.kwargs
        assert call_kwargs["failure_count"] == 1

    def test_run_collection_batch_post_error_counts_as_failed(self) -> None:
        client = MagicMock(spec=BackendClient)
        client.post_program_batch.side_effect = RuntimeError("backend error")
        client.post_job_log.return_value = {"id": 1}
        fetcher = make_fetcher(entries=[_ENTRY_PROGRAM])

        run_program_collection(client, fetcher, [_OSHI_A])

        call_kwargs = client.post_job_log.call_args.kwargs
        assert call_kwargs["failure_count"] == 1

    def test_run_collection_batch_post_error_continues_to_next_oshi(self) -> None:
        client = MagicMock(spec=BackendClient)
        client.post_program_batch.side_effect = [
            RuntimeError("backend error"),
            _BATCH_RESULT_SAVED,
        ]
        client.post_job_log.return_value = {"id": 1}
        fetcher = MagicMock(spec=ProgramFetcher)
        fetcher.fetch_for_oshi.return_value = [_ENTRY_PROGRAM]

        run_program_collection(client, fetcher, [_OSHI_A, _OSHI_B])

        assert client.post_program_batch.call_count == 2

    # ── ジョブログ送信 ────────────────────────────────────────────────────────

    def test_run_collection_sends_job_log_with_saved_count(self) -> None:
        batch_result = BatchResult(received_count=2, saved_count=2, skipped_count=0)
        client = make_client(batch_result=batch_result)
        fetcher = make_fetcher(entries=[_ENTRY_PROGRAM, _ENTRY_DRAMA])

        run_program_collection(client, fetcher, [_OSHI_A])

        client.post_job_log.assert_called_once()
        call_kwargs = client.post_job_log.call_args.kwargs
        assert call_kwargs["job_type"] == "program"
        assert call_kwargs["success_count"] == 2
        assert call_kwargs["failure_count"] == 0

    def test_run_collection_job_log_failure_does_not_raise(self) -> None:
        client = make_client()
        client.post_job_log.side_effect = RuntimeError("log API down")
        fetcher = make_fetcher(entries=[_ENTRY_PROGRAM])

        # ジョブログ送信の失敗は例外として上に伝播しないこと
        run_program_collection(client, fetcher, [_OSHI_A])

    def test_run_collection_multiple_oshis_totals_accumulated(self) -> None:
        """複数の推しのバッチ結果が合算される"""
        client = MagicMock(spec=BackendClient)
        client.post_program_batch.return_value = BatchResult(
            received_count=1, saved_count=1, skipped_count=0
        )
        client.post_job_log.return_value = {"id": 1}
        fetcher = make_fetcher(entries=[_ENTRY_PROGRAM])

        run_program_collection(client, fetcher, [_OSHI_A, _OSHI_B])

        call_kwargs = client.post_job_log.call_args.kwargs
        assert call_kwargs["success_count"] == 2  # 2推し × saved_count=1

    # ── FEAT-036: source_name の伝播 ─────────────────────────────────────────

    def test_run_collection_batch_items_contain_source_name(self) -> None:
        """ProgramEntry の source_name が ProgramBatchItem に引き継がれる"""
        entry_with_source = ProgramEntry(
            program_name="推しA 出演番組 - Weverse",
            source_url="https://weverse.io/prog",
            aired_at=_NOW,
            broadcaster=None,
            source_name="Weverse",
        )
        client = MagicMock(spec=BackendClient)
        client.post_program_batch.return_value = BatchResult(
            received_count=1, saved_count=1, skipped_count=0
        )
        client.post_job_log.return_value = {"id": 1}
        fetcher = MagicMock(spec=ProgramFetcher)
        fetcher.fetch_for_oshi.return_value = [entry_with_source]

        run_program_collection(client, fetcher, [_OSHI_A])

        items: list[ProgramBatchItem] = client.post_program_batch.call_args.kwargs["items"]
        assert len(items) == 1
        assert items[0].source_name == "Weverse"

    def test_run_collection_batch_items_source_name_none_when_absent(self) -> None:
        """source_name が None の場合もエラーにならず None として渡す"""
        entry_no_source = ProgramEntry(
            program_name="推しA 出演番組情報",
            source_url="https://example.com/prog-no-source",
            aired_at=_NOW,
            broadcaster=None,
            source_name=None,
        )
        client = MagicMock(spec=BackendClient)
        client.post_program_batch.return_value = BatchResult(
            received_count=1, saved_count=1, skipped_count=0
        )
        client.post_job_log.return_value = {"id": 1}
        fetcher = MagicMock(spec=ProgramFetcher)
        fetcher.fetch_for_oshi.return_value = [entry_no_source]

        run_program_collection(client, fetcher, [_OSHI_A])

        items: list[ProgramBatchItem] = client.post_program_batch.call_args.kwargs["items"]
        assert len(items) == 1
        assert items[0].source_name is None
