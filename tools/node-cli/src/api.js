// Minimal Granola public API client (https://docs.granola.ai/api-reference/openapi.json)
// Rate limit is 25 req / 5s burst, 5 req/s sustained, so requests are spaced out and 429s retried.

const BASE_URL = 'https://public-api.granola.ai/v1';
const MIN_INTERVAL_MS = 250;
const MAX_RETRIES = 5;

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

class GranolaApiError extends Error {
  constructor(status, body, url) {
    super(`Granola API ${status} for ${url}: ${typeof body === 'string' ? body : JSON.stringify(body)}`);
    this.status = status;
    this.body = body;
  }
}

class GranolaClient {
  constructor(apiKey) {
    if (!apiKey) throw new Error('Missing Granola API key. Set GRANOLA_API_KEY or "apiKey" in config.json.');
    this.apiKey = apiKey;
    this.lastRequestAt = 0;
  }

  async request(path, query = {}) {
    const url = new URL(BASE_URL + path);
    for (const [k, v] of Object.entries(query)) {
      if (v !== undefined && v !== null && v !== '') url.searchParams.set(k, v);
    }

    for (let attempt = 0; ; attempt++) {
      const wait = this.lastRequestAt + MIN_INTERVAL_MS - Date.now();
      if (wait > 0) await sleep(wait);
      this.lastRequestAt = Date.now();

      const res = await fetch(url, { headers: { Authorization: `Bearer ${this.apiKey}`, Accept: 'application/json' } });
      if (res.ok) return res.json();

      const text = await res.text();
      let body;
      try { body = JSON.parse(text); } catch { body = text; }

      const retryable = res.status === 429 || res.status >= 500;
      if (retryable && attempt < MAX_RETRIES) {
        const retryAfter = Number(res.headers.get('retry-after'));
        await sleep(retryAfter > 0 ? retryAfter * 1000 : 1000 * 2 ** attempt);
        continue;
      }
      throw new GranolaApiError(res.status, body, url.toString());
    }
  }

  // Yields every NoteSummary, following cursors.
  async *listNotes(filters = {}) {
    let cursor;
    do {
      const page = await this.request('/notes', { ...filters, page_size: 30, cursor });
      for (const note of page.notes) yield note;
      cursor = page.hasMore ? page.cursor : null;
    } while (cursor);
  }

  // Full note with transcript; falls back to the paged transcript endpoint for large transcripts.
  async getNoteWithTranscript(noteId) {
    try {
      return await this.request(`/notes/${noteId}`, { include: 'transcript' });
    } catch (err) {
      if (err.status !== 413) throw err;
    }
    const note = await this.request(`/notes/${noteId}`);
    note.transcript = await this.getTranscript(noteId);
    return note;
  }

  async getTranscript(noteId) {
    const items = [];
    let cursor;
    do {
      const page = await this.request(`/notes/${noteId}/transcript`, { page_size: 100, cursor });
      items.push(...page.transcript);
      cursor = page.hasMore ? page.cursor : null;
    } while (cursor);
    return items;
  }
}

module.exports = { GranolaClient, GranolaApiError };
