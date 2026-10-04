import { z } from "zod";

/** GET /api/v1/bookmarks の単一アイテム */
export const bookmarkResponseSchema = z.object({
  id: z.number().int(),
  newsItemId: z.number().int(),
  oshiId: z.number().int(),
  oshiName: z.string(),
  title: z.string(),
  summary: z.string().nullable(),
  sourceUrl: z.string().url(),
  publishedAt: z.string().datetime({ offset: true }).nullable(),
  isPublishedAtEstimated: z.boolean(),
  collectedAt: z.string().datetime({ offset: true }),
  bookmarkedAt: z.string().datetime({ offset: true }),
  /** 媒体名（FEAT-036）。タイトル末尾から取り出した値またはnull */
  sourceName: z.string().nullable(),
});

export type BookmarkResponse = z.infer<typeof bookmarkResponseSchema>;

/** ページネーションレスポンス */
export const bookmarksPaginatedSchema = z.object({
  items: z.array(bookmarkResponseSchema),
  totalCount: z.number().int(),
  page: z.number().int(),
  pageSize: z.number().int(),
  totalPages: z.number().int(),
});

export type BookmarksPaginatedResponse = z.infer<
  typeof bookmarksPaginatedSchema
>;
