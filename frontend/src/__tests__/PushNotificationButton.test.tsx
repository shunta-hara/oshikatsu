import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { PushNotificationButton } from "../components/push/PushNotificationButton";
import * as pushApi from "../api/push";
import { ApiError } from "../api/client";

vi.mock("../api/push");

const mockGetVapidPublicKey = vi.mocked(pushApi.getVapidPublicKey);
const mockRegisterPushSubscription = vi.mocked(
  pushApi.registerPushSubscription,
);
const mockDeletePushSubscription = vi.mocked(pushApi.deletePushSubscription);

/** 既存購読なしのデフォルト mock サブスクリプション */
function makeMockSubscription(endpoint = "https://push.example.com/sub") {
  return {
    endpoint,
    getKey: (keyType: string): ArrayBuffer | null => {
      if (keyType === "p256dh") return new Uint8Array(65).buffer;
      if (keyType === "auth") return new Uint8Array(16).buffer;
      return null;
    },
    unsubscribe: vi.fn().mockResolvedValue(true),
  };
}

/** 対応ブラウザのモックを navigator / window に設定する */
function setupSupportedBrowser(
  options: {
    permission?: NotificationPermission;
    existingSubscription?: ReturnType<typeof makeMockSubscription> | null;
    requestPermissionResult?: NotificationPermission;
  } = {},
) {
  const {
    permission = "default",
    existingSubscription = null,
    requestPermissionResult = "granted",
  } = options;

  const newSubscription = makeMockSubscription();

  const mockPushManager = {
    getSubscription: vi.fn().mockResolvedValue(existingSubscription),
    subscribe: vi.fn().mockResolvedValue(newSubscription),
  };

  const mockRegistration = {
    pushManager: mockPushManager,
  };

  vi.stubGlobal("Notification", {
    permission,
    requestPermission: vi.fn().mockResolvedValue(requestPermissionResult),
  });
  vi.stubGlobal("PushManager", {});

  Object.defineProperty(navigator, "serviceWorker", {
    value: {
      register: vi.fn().mockResolvedValue(mockRegistration),
      ready: Promise.resolve(mockRegistration),
    },
    writable: true,
    configurable: true,
  });

  return { mockPushManager, mockRegistration, newSubscription };
}

/** navigator.serviceWorker を削除してブラウザ非対応状態に戻す */
function teardownSupportedBrowser() {
  vi.unstubAllGlobals();
  try {
    delete (window.navigator as unknown as Record<string, unknown>)
      .serviceWorker;
  } catch {
    // 削除できない場合は undefined に設定（isPushSupported は PushManager チェックで false になる）
    Object.defineProperty(navigator, "serviceWorker", {
      value: undefined,
      writable: true,
      configurable: true,
    });
  }
}

// ── 非対応ブラウザ（serviceWorker 未定義） ───────────────────────────────────

describe("PushNotificationButton - 非対応ブラウザ", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it("Push非対応ブラウザではコンポーネントが何も表示しない", () => {
    // jsdom デフォルトは serviceWorker / PushManager が未定義
    render(<PushNotificationButton />);
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
    expect(screen.queryByText("通知を受け取る")).not.toBeInTheDocument();
    expect(screen.queryByText("通知を解除する")).not.toBeInTheDocument();
  });
});

// ── 対応ブラウザ ─────────────────────────────────────────────────────────────

describe("PushNotificationButton - 対応ブラウザ", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    mockGetVapidPublicKey.mockResolvedValue(
      "dGVzdC12YXBpZC1wdWJsaWMta2V5LWZvcnRlc3Rpbmc",
    );
    mockRegisterPushSubscription.mockResolvedValue(undefined);
    mockDeletePushSubscription.mockResolvedValue(undefined);
  });

  afterEach(() => {
    teardownSupportedBrowser();
  });

  // ── 正常系: 通知未設定・未購読 ─────────────────────────────────────────────

  it("通知未設定（default）のとき「通知を受け取る」ボタンが表示される", async () => {
    setupSupportedBrowser({ permission: "default" });
    render(<PushNotificationButton />);

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "通知を受け取る" }),
      ).toBeInTheDocument();
    });
  });

  it("「通知を受け取る」ボタンクリックで購読登録 API が呼ばれる", async () => {
    const user = userEvent.setup();
    const { mockPushManager } = setupSupportedBrowser();
    render(<PushNotificationButton />);

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "通知を受け取る" }),
      ).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: "通知を受け取る" }));

    await waitFor(() => {
      expect(mockGetVapidPublicKey).toHaveBeenCalledOnce();
      expect(mockPushManager.subscribe).toHaveBeenCalledOnce();
      expect(mockRegisterPushSubscription).toHaveBeenCalledOnce();
    });
  });

  it("購読成功後にボタンが「通知を解除する」に変わる", async () => {
    const user = userEvent.setup();
    setupSupportedBrowser();
    render(<PushNotificationButton />);

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "通知を受け取る" }),
      ).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: "通知を受け取る" }));

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "通知を解除する" }),
      ).toBeInTheDocument();
    });
  });

  // ── 正常系: 購読済み状態 ────────────────────────────────────────────────────

  it("既存購読がある場合は「通知を解除する」ボタンが表示される", async () => {
    const existingSubscription = makeMockSubscription();
    setupSupportedBrowser({ existingSubscription });
    render(<PushNotificationButton />);

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "通知を解除する" }),
      ).toBeInTheDocument();
    });
  });

  it("「通知を解除する」クリックで購読削除 API と unsubscribe が呼ばれる", async () => {
    const user = userEvent.setup();
    const existingSubscription = makeMockSubscription();
    setupSupportedBrowser({ existingSubscription });
    render(<PushNotificationButton />);

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "通知を解除する" }),
      ).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: "通知を解除する" }));

    await waitFor(() => {
      expect(mockDeletePushSubscription).toHaveBeenCalledWith(
        existingSubscription.endpoint,
      );
      expect(existingSubscription.unsubscribe).toHaveBeenCalledOnce();
    });
  });

  it("購読解除後にボタンが「通知を受け取る」に戻る", async () => {
    const user = userEvent.setup();
    const existingSubscription = makeMockSubscription();
    setupSupportedBrowser({ existingSubscription });
    render(<PushNotificationButton />);

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "通知を解除する" }),
      ).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: "通知を解除する" }));

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "通知を受け取る" }),
      ).toBeInTheDocument();
    });
  });

  // ── 異常系: 通知ブロック ────────────────────────────────────────────────────

  it("通知ブロック済み（denied）のとき案内メッセージが表示される", () => {
    setupSupportedBrowser({ permission: "denied" });
    render(<PushNotificationButton />);

    expect(
      screen.getByText("ブラウザ設定から許可してください"),
    ).toBeInTheDocument();
  });

  it("通知ブロック済みのときボタンが表示されない", () => {
    setupSupportedBrowser({ permission: "denied" });
    render(<PushNotificationButton />);

    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });

  it("購読フロー中に通知を拒否すると案内メッセージが表示される", async () => {
    const user = userEvent.setup();
    setupSupportedBrowser({ requestPermissionResult: "denied" });
    render(<PushNotificationButton />);

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "通知を受け取る" }),
      ).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: "通知を受け取る" }));

    await waitFor(() => {
      expect(
        screen.getByText("ブラウザ設定から許可してください"),
      ).toBeInTheDocument();
    });
  });

  // ── 異常系: API エラー ──────────────────────────────────────────────────────

  it("購読登録 API がエラーを返すとエラーメッセージが表示される", async () => {
    const user = userEvent.setup();
    setupSupportedBrowser();
    mockRegisterPushSubscription.mockRejectedValueOnce(
      new ApiError(500, "Internal Server Error"),
    );
    render(<PushNotificationButton />);

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "通知を受け取る" }),
      ).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: "通知を受け取る" }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent(
        "Internal Server Error",
      );
    });
  });

  it("購読削除 API がエラーを返すとエラーメッセージが表示される", async () => {
    const user = userEvent.setup();
    const existingSubscription = makeMockSubscription();
    setupSupportedBrowser({ existingSubscription });
    mockDeletePushSubscription.mockRejectedValueOnce(
      new ApiError(500, "削除エラー"),
    );
    render(<PushNotificationButton />);

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "通知を解除する" }),
      ).toBeInTheDocument();
    });

    await user.click(screen.getByRole("button", { name: "通知を解除する" }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent("削除エラー");
    });
  });

  // ── 境界値 ─────────────────────────────────────────────────────────────────

  it("購読中はボタンが「処理中...」になり無効化される", async () => {
    setupSupportedBrowser();
    // registerPushSubscription を resolve しない（pending）
    mockRegisterPushSubscription.mockImplementation(
      () => new Promise<void>(() => undefined),
    );
    render(<PushNotificationButton />);

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "通知を受け取る" }),
      ).toBeInTheDocument();
    });

    // fireEvent で中間状態をテスト（userEvent は全 microtask を flush するため）
    const { fireEvent } = await import("@testing-library/react");
    fireEvent.click(screen.getByRole("button", { name: "通知を受け取る" }));

    await waitFor(() => {
      expect(screen.getByRole("button", { name: "処理中..." })).toBeDisabled();
    });
  });
});
