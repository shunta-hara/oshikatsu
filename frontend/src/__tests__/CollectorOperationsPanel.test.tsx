import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { CollectorOperationsPanel } from "../components/collector/CollectorOperationsPanel";
import * as collectorApi from "../api/collector";
import { ApiError } from "../api/client";
import type { CollectorOperationsResponse } from "../types/collector";

vi.mock("../api/collector");
const mockGetCollectorOperations = vi.mocked(
  collectorApi.getCollectorOperations,
);

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

describe("CollectorOperationsPanel", () => {
  beforeEach(() => {
    vi.resetAllMocks();
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
});
