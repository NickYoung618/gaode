import type { HttpClient } from '../api/http-client.ts';
export function mediaUrl(baseUrl: string, mediaId: string): string {
  if (!/^[0-9a-f-]{36}$/i.test(mediaId)) throw new Error('Invalid mediaId');
  return `${baseUrl.replace(/\/$/, '')}/api/v1/station01/media/${mediaId}`;
}
export async function readMedia(http: HttpClient, mediaId: string, etag?: string) { return http.get(`/api/v1/station01/media/${mediaId}`, etag); }
