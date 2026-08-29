import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { CollectorStatus } from "../components/collector/CollectorStatus";
import * as collectorApi from "../api/collector";
import { ApiError } from "../api/client";
import type { CollectorStatusResponse } from "../types/collector";

vi.mock("../api/collector");
const mockGetCollectorStatus = vi.mocked(collectorApi.getCollectorStatus);

function makeStatus(
  overrides: Partial<CollectorStatusResponse> = {},
): CollectorStatusResponse {
  return {
    latestNewsJob: null,
    latestProgramJob: null,
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

describe("CollectorStatus", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  // ── 正常系 ─────────────────────────────────────────────────────────────

  it("ニュース収集の最終実行情報が表示される", async () => {
    mockGetCollectorStatus.mockResolvedValue(
      makeStatus({
        latestNewsJob: makeJobLog({
          jobType: "news",
          successCount: 10,
          failureCount: 2,
        }),
      }),
    );

    render(<CollectorStatus />);

    await waitFor(() => {
      expect(screen.getByText("ニュース収集 最終実行")).toBeInTheDocument();
      expect(screen.getByText(/成功: 10件/)).toBeInTheDocument();
      expect(screen.getByText(/失敗: 2件/)).toBeInTheDocument();
    });
  });

  it("番組収集の最終実行情報が表示される", async () => {
    mockGetCollectorStatus.mockResolvedValue(
      makeStatus({
        latestProgramJob: makeJobLog({
          jobType: "program",
          successCount: 3,
          failureCount: 1,
        }),
      }),
    );

    render(<CollectorStatus />);

    await waitFor(() => {
      expect(screen.getByText("番組収集 最終実行")).toBeInTheDocument();
      expect(screen.getByText(/成功: 3件/)).toBeInTheDocument();
      expect(screen.getByText(/失敗: 1件/)).toBeInTheDocument();
    });
  });

  // ── 異常系 ─────────────────────────────────────────────────────────────

  it("APIエラー時にエラーメッセージが表示される", async () => {
    mockGetCollectorStatus.mockRejectedValue(
      new ApiError(500, "Internal Server Error"),
    );

    render(<CollectorStatus />);

    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
    });
  });

  it("ネットワークエラー時にエラーメッセージが表示される", async () => {
    mockGetCollectorStatus.mockRejectedValue(new Error("Network Error"));

    render(<CollectorStatus />);

    await waitFor(() => {
      expect(
        screen.getByText("収集ジョブ状況の取得に失敗しました"),
      ).toBeInTheDocument();
    });
  });

  // ── 境界値 ─────────────────────────────────────────────────────────────

  it("ニュース収集が未実行のとき「未実行」が表示される", async () => {
    mockGetCollectorStatus.mockResolvedValue(
      makeStatus({ latestNewsJob: null }),
    );

    render(<CollectorStatus />);

    await waitFor(() => {
      const items = screen.getAllByText("未実行");
      expect(items.length).toBeGreaterThanOrEqual(1);
    });
  });

  it("番組収集が未実行のとき「未実行」が表示される", async () => {
    mockGetCollectorStatus.mockResolvedValue(
      makeStatus({ latestProgramJob: null }),
    );

    render(<CollectorStatus />);

    await waitFor(() => {
      const items = screen.getAllByText("未実行");
      expect(items.length).toBeGreaterThanOrEqual(1);
    });
  });

  it("両方未実行のとき「未実行」が2件表示される", async () => {
    mockGetCollectorStatus.mockResolvedValue(makeStatus());

    render(<CollectorStatus />);

    await waitFor(() => {
      expect(screen.getAllByText("未実行")).toHaveLength(2);
    });
  });

  it("ローディング中はテキストが表示される", () => {
    mockGetCollectorStatus.mockImplementation(
      () => new Promise(() => undefined),
    );

    render(<CollectorStatus />);

    expect(screen.getByText("読み込み中...")).toBeInTheDocument();
  });
});
