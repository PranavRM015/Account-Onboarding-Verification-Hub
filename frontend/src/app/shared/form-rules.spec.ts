import { connectionErrorMessage } from './form-rules';

describe('Connection error feedback', () => {
  for (const status of [0, 502, 503, 504]) {
    it(`explains an unavailable connection for status ${status}`, () => {
      expect(connectionErrorMessage({ status })).toBe('Unable to connect to the server. Please try again shortly.');
    });
  }

  for (const status of [400, 401, 403, 409, 423, 429, 500]) {
    it(`keeps the existing error handling for status ${status}`, () => {
      expect(connectionErrorMessage({ status })).toBeNull();
    });
  }
});
