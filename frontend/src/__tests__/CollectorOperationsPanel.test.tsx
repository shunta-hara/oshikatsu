import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { CollectorOperationsPanel } from "../components/collector/CollectorOperationsPanel";
import * as collectorApi from "../api/collector";
import * as healthApi from "../api/health";
import { ApiError } from "../api/client";
import type { CollectorOperationsResponse } from "../types/collector";
import type { HealthResponse } from "../types/health";

vi.mock("../api/collector");
vi.mock("../api/health");

const mockGetCollectorOperations = vi.mocked(
  collectorApi.getCollectorOperations,
);
const mockGetHealth = vi.mocked(healthApi.getHealth);

function makeOperations(
  overrides: Partial<CollectorOperationsResponse> = {},
): CollectorOperationsResponse {
  return {
    recentJobs: [],
    isOverallJobFailing: false,
    failingTalents: [],
    ...overrides,
  };
}

function makeJobLog(overrides: object = {}) {
  return {
    id: 1,
    jobType: "news",
    executedAt: "2026-07-17T10:00:00+09:00",
    successCount: 5,
    failureCount: 0,
    createdAt: "2026-07-17T10:00:00+09:00",
    ...overrides,
  };
}

function makeHealth(overrides: Partial<HealthResponse> = {}): HealthResponse {
  return {
    status: "ok",
    timestamp: "2026-10-06T00:00:00.000Z",
    dbSizeBytes: 209_715_200,
    dbSizeMb: 200.0,
    ...overrides,
  };
}

describe("CollectorOperationsPanel", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    // デフォルト: DB 使用量あり（200 MB）
    mockGetHealth.mockResolvedValue(makeHealth());
  });

  // ── 正常系 ─────────────────────────────────────────────────────────────

  it("直近24時間のジョブ実行セクションが表示される", async () => {
    mockGetCollectorOperations.mockResolvedValue(makeOperations());

    render(<CollectorOperationsPanel />);

    await waitFor(() => {
      expect(screen.getByText("直近24時間のジョブ実行")).toBeInTheDocument();
    });
  });

  it("ジョブが存在しない場合「直近24時間の実行記録なし」が表示される", async () => {
    mockGetCollectorOperations.mockResolvedValue(
      makeOperations({ recentJobs: [] }),
    );

    render(<CollectorOperationsPanel />);

    await waitFor(() => {
      expect(screen.getByText("直近24時間の実行記録なし")).toBeInTheDocument();
    });
  });

  it("ジョブが存在する場合、成功件数・失敗件数が表示される", async () => {
    mockGetCollectorOperations.mockResolvedValue(
      makeOperations({
        recentJobs: [
          makeJobLog({ jobType: "news", successCount: 10, failureCount: 2 }),
        ],
      }),
    );

    render(<CollectorOperationsPanel />);

    await waitFor(() => {
      expect(screen.getByText(/成功: 10件/)).toBeInTheDocument();
      expect(screen.getByText(/失敗: 2件/)).toBeInTheDocument();
    });
  });

  it("全体収集ジョブが失敗中の場合、アラートが表示される", async () => {
    mockGetCollectorOperations.mockResolvedValue(
      makeOperations({ isOverallJobFailing: true }),
    );

    render(<CollectorOperationsPanel />);

    await waitFor(() => {
      expect(
        screen.getByText("全体収集ジョブが現在失敗中です"),
      ).toBeInTheDocument();
    });
  });

  it("失敗中のタレントがある場合、名前と連続失敗回数が表示される", async () => {
    mockGetCollectorOperations.mockResolvedValue(
      makeOperations({
        failingTalents: [
          { talentId: 1, talentName: "推しA", consecutiveFailureCount: 3 },
          { talentId: 2, talentName: "推しB", consecutiveFailureCount: 5 },
        ],
      }),
    );

    render(<CollectorOperationsPanel />);

    await waitFor(() => {
      expect(screen.getByText("推しA")).toBeInTheDocument();
      expect(screen.getByText("3回連続失敗中")).toBeInTheDocument();
      expect(screen.getByText("推しB")).toBeInTheDocument();
      expect(screen.getByText("5回連続失敗中")).toBeInTheDocument();
    });
  });

  it("失敗なし・タレント失敗なしの場合、アラートが表示されない", async () => {
    mockGetCollectorOperations.mockResolvedValue(
      makeOperations({
        isOverallJobFailing: false,
        failingTalents: [],
      }),
    );

    render(<CollectorOperationsPanel />);

    await waitFor(() => {
      expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    });
  });

  // ── 異常系 ─────────────────────────────────────────────────────────────

  it("APIエラー時にエラーメッセージが表示される", async () => {
    mockGetCollectorOperations.mockRejectedValue(
      new ApiError(500, "Internal Server Error"),
    );

    render(<CollectorOperationsPanel />);

    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
      expect(screen.getByText("Internal Server Error")).toBeInTheDocument();
    });
  });

  it("ネットワークエラー時にフォールバックメッセージが表示される", async () => {
    mockGetCollectorOperations.mockRejectedValue(new Error("Network Error"));

    render(<CollectorOperationsPanel />);

    await waitFor(() => {
      expect(
        screen.getByText("運用パネルの取得に失敗しました"),
      ).toBeInTheDocument();
    });
  });

  // ── 境界値 ─────────────────────────────────────────────────────────────

  it("ローディング中はテキストが表示される", () => {
    mockGetCollectorOperations.mockImplementation(
      () => new Promise(() => undefined),
    );

    render(<CollectorOperationsPanel />);

    expect(screen.getByText("読み込み中...")).toBeInTheDocument();
  });

  it("複数のジョブが最新順に表示される", async () => {
    mockGetCollectorOperations.mockResolvedValue(
      makeOperations({
        recentJobs: [
          makeJobLog({
            id: 1,
            jobType: "news",
            executedAt: "2026-07-17T12:00:00+09:00",
            successCount: 3,
            failureCount: 0,
          }),
          makeJobLog({
            id: 2,
            jobType: "program",
            executedAt: "2026-07-17T10:00:00+09:00",
            successCount: 2,
            failureCount: 1,
          }),
        ],
      }),
    );

    render(<CollectorOperationsPanel />);

    await waitFor(() => {
      // 両方のジョブタイプが表示される
      const newsItems = screen.getAllByText("news");
      const programItems = screen.getAllByText("program");
      expect(newsItems.length).toBeGreaterThanOrEqual(1);
      expect(programItems.length).toBeGreaterThanOrEqual(1);
    });
  });

  it("全体失敗かつタレント失敗の両方がある場合、どちらも表示される", async () => {
    mockGetCollectorOperations.mockResolvedValue(
      makeOperations({
        isOverallJobFailing: true,
        failingTalents: [
          { talentId: 1, talentName: "推しC", consecutiveFailureCount: 4 },
        ],
      }),
    );

    render(<CollectorOperationsPanel />);

    await waitFor(() => {
      expect(
        screen.getByText("全体収集ジョブが現在失敗中です"),
      ).toBeInTheDocument();
      expect(screen.getByText("推しC")).toBeInTheDocument();
      expect(screen.getByText("4回連続失敗中")).toBeInTheDocument();
    });
  });

  // ── DB 使用量表示（FEAT-053）──────────────────────────────────────────────

  it("DB 使用量セクションが表示される", async () => {
    mockGetCollectorOperations.mockResolvedValue(makeOperations());
    mockGetHealth.mockResolvedValue(makeHealth({ dbSizeMb: 200.0 }));

    render(<CollectorOperationsPanel />);

    await waitFor(() => {
      expect(screen.getByText("Neon DB 使用量")).toBeInTheDocument();
    });
  });

  it("DB サイズが取得できたとき MB と % が表示される", async () => {
    mockGetCollectorOperations.mockResolvedValue(makeOperations());
    // 200 MB / 500 MB = 40%
    mockGetHealth.mockResolvedValue(makeHealth({ dbSizeMb: 200.0 }));

    render(<CollectorOperationsPanel />);

    await waitFor(() => {
      expect(screen.getByText(/200 MB \/ 500 MB（40%）/)).toBeInTheDocument();
    });
  });

  it("DB サイズが 400 MB 超のとき正しい % が表示される（閾値近辺）", async () => {
    mockGetCollectorOperations.mockResolvedValue(makeOperations());
    // 450 MB / 500 MB = 90%
    mockGetHealth.mockResolvedValue(makeHealth({ dbSizeMb: 450.0 }));

    render(<CollectorOperationsPanel />);

    await waitFor(() => {
      expect(screen.getByText(/450 MB \/ 500 MB（90%）/)).toBeInTheDocument();
    });
  });

  it("progressbar に DB 使用率が設定される", async () => {
    mockGetCollectorOperations.mockResolvedValue(makeOperations());
    mockGetHealth.mockResolvedValue(makeHealth({ dbSizeMb: 250.0 })); // 50%

    render(<CollectorOperationsPanel />);

    await waitFor(() => {
      const progressBar = screen.getByRole("progressbar", {
        name: "DB 使用率",
      });
      expect(progressBar).toHaveAttribute("aria-valuenow", "50");
    });
  });

  it("DB サイズが null のとき「取得できませんでした」が表示される", async () => {
    mockGetCollectorOperations.mockResolvedValue(makeOperations());
    mockGetHealth.mockResolvedValue(
      makeHealth({ dbSizeMb: null, dbSizeBytes: null }),
    );

    render(<CollectorOperationsPanel />);

    await waitFor(() => {
      expect(
        screen.getByText("DB 使用量を取得できませんでした"),
      ).toBeInTheDocument();
    });
  });

  it("getHealth が失敗したとき「取得できませんでした」が表示される（サイレントエラー）", async () => {
    mockGetCollectorOperations.mockResolvedValue(makeOperations());
    mockGetHealth.mockRejectedValue(new Error("Network error"));

    render(<CollectorOperationsPanel />);

    await waitFor(() => {
      expect(
        screen.getByText("DB 使用量を取得できませんでした"),
      ).toBeInTheDocument();
    });
  });
});
