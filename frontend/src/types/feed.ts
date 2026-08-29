import { z } from "zod";

/** GET /api/v1/news の単一アイテム */
export const newsItemSchema = z.object({
  id: z.number().int(),
  oshiId: z.number().int(),
  oshiName: z.string(),
  title: z.string(),
  summary: z.string().nullable(),
  sourceUrl: z.string().url(),
  publishedAt: z.string().datetime({ offset: true }).nullable(),
  isPublishedAtEstimated: z.boolean(),
  collectedAt: z.string().datetime({ offset: true }),
  /** ログインユーザーがこの記事をブックマーク済みかどうか（未取得の場合は undefined）*/
  isBookmarked: z.boolean().optional(),
});

export type NewsItem = z.infer<typeof newsItemSchema>;

/** GET /api/v1/programs の単一アイテム */
export const programAppearanceSchema = z.object({
  id: z.number().int(),
  oshiId: z.number().int(),
  oshiName: z.string(),
  programName: z.string(),
  broadcaster: z.string().nullable(),
  airedAt: z.string().datetime({ offset: true }).nullable(),
  isAiredAtEstimated: z.boolean(),
  sourceUrl: z.string().url(),
  collectedAt: z.string().datetime({ offset: true }),
});

export type ProgramAppearance = z.infer<typeof programAppearanceSchema>;

/** ページネーションレスポンス汎用スキーマファクトリ */
const makePaginatedSchema = <T extends z.ZodTypeAny>(itemSchema: T) =>
  z.object({
    items: z.array(itemSchema),
    totalCount: z.number().int(),
    page: z.number().int(),
    pageSize: z.number().int(),
    totalPages: z.number().int(),
  });

export const newsPaginatedSchema = makePaginatedSchema(newsItemSchema);
export type NewsPaginatedResponse = z.infer<typeof newsPaginatedSchema>;

export const programsPaginatedSchema = makePaginatedSchema(
  programAppearanceSchema,
);
export type ProgramsPaginatedResponse = z.infer<typeof programsPaginatedSchema>;

/** GET /api/v1/feed/unread-count のレスポンス */
export const unreadCountSchema = z.object({
  unreadCount: z.number().int().nonnegative(),
});

export type UnreadCountResponse = z.infer<typeof unreadCountSchema>;
