// Load and soak check for the production container. It runs in CI against the container that the smoke job starts.
// The arrival rate is constant, so a slowdown shows up as latency rather than as fewer requests.
// Run locally with: docker run --rm --network host -v "$PWD/tests/load:/scripts:ro" -e BASE_URL=http://localhost:8080 grafana/k6 run /scripts/aurora-smoke.js
import http from 'k6/http';
import { check } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';
const DURATION = __ENV.DURATION || '2m';
const RATE_PER_SECOND = Number(__ENV.RATE || 20);
const PRE_ALLOCATED_USERS = 20;
const MAX_USERS = 100;
const P95_LIMIT_MS = 800;
const MAX_FAILURE_RATE = 0.01;
const MIN_CHECK_PASS_RATE = 0.99;
const HTTP_OK = 200;
const HTTP_FOUND = 302;

export const options = {
  scenarios: {
    steady: {
      executor: 'constant-arrival-rate',
      rate: RATE_PER_SECOND,
      timeUnit: '1s',
      duration: DURATION,
      preAllocatedVUs: PRE_ALLOCATED_USERS,
      maxVUs: MAX_USERS,
    },
  },
  thresholds: {
    http_req_failed: [`rate<${MAX_FAILURE_RATE}`],
    http_req_duration: [`p(95)<${P95_LIMIT_MS}`],
    checks: [`rate>${MIN_CHECK_PASS_RATE}`],
  },
};

export default function () {
  const ready = http.get(`${BASE_URL}/health/ready`);
  check(ready, { 'readiness is 200': (r) => r.status === HTTP_OK });

  const login = http.get(`${BASE_URL}/Account/Login`, { redirects: 0 });
  check(login, { 'sign-in page is 200': (r) => r.status === HTTP_OK });

  // Anonymous access to a protected page must redirect to sign-in, under load as well as at rest.
  const guarded = http.get(`${BASE_URL}/Reporting/Export`, { redirects: 0 });
  check(guarded, { 'anonymous export redirects to sign-in': (r) => r.status === HTTP_FOUND });
}
