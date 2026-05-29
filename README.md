# Bot Detection & Mitigation Middleware

## Overview

This project implements a configurable bot detection and mitigation middleware for ASP.NET Core.

The solution analyzes incoming requests using multiple detection signals, calculates a risk score, classifies traffic, and applies appropriate mitigation actions.

The design focuses on:

* Extensibility
* Configurability
* Performance
* Observability
* Multi-layer architecture

The implementation follows an N-Layer Architecture and is designed to support future distributed deployments using Redis-backed rate limiting.
## Architecture

Request
↓
BotDetectionMiddleware
↓
TrafficClassifier
↓
DetectionEngine
↓
Detection Rules
├── UserAgentRule
├── HeaderAnalysisRule
└── RateLimitRule
↓
Score Calculation
↓
Action Mapping
↓
Allow / Shadow / Challenge / Throttle / Block
## Implemented Features

### Detection Signals

#### User Agent Analysis

Detects:

* Missing User-Agent
* Suspicious User-Agent values

  * curl
  * wget
  * python-requests
  * scrapy
  * aiohttp

#### Header Analysis

Detects:

* Missing Accept-Language
* Missing Accept-Encoding

#### Rate Limiting

Supports:

* Per-IP request tracking
* Configurable request limits
* In-memory implementation

### Traffic Classification

Supported classifications:

* Internal Services
* API Partners
* Anonymous Users
* Known Abusers

### Response Actions

Implemented actions:

* Allow
* Shadow
* Challenge
* Throttle
* Block

### Observability

* Structured audit logging
* Detection result logging
* Debug endpoint

### Configuration

Configurable via appsettings.json:

* Rule weights
* Detection thresholds
* Rate limits
* Traffic classification settings
## Configuration

## API Endpoints

GET /api/test
GET /api/debug
GET /health

## Sliding Window Rate Limiting Design

This middleware implements a production-grade, distributed sliding window rate limiter backed by **Redis** (with seamless fallback to **in-memory caching**).

### 1. Sliding Window Algorithm (Redis Sorted Sets)
To protect against boundary attacks (where traffic spikes around fixed minute boundaries), we implement a true sliding window using **Redis Sorted Sets (ZSets)**:
- **Keys**: Every client context and endpoint has a unique key (e.g. `ratelimit:rate:client:192.168.1.1:hash:path`).
- **Values**: Each request timestamp (in milliseconds) is added as a member in the sorted set. The score of the member is also the millisecond timestamp.
- **Atomicity (Lua Scripting)**: To prevent race conditions from concurrent API instances, the checking and updating is done atomically in a single Redis Lua script:
  1. Evict expired entries: `ZREMRANGEBYSCORE key 0 (now - window)`
  2. Count remaining entries: `ZCARD key`
  3. If count < limit, add the current request: `ZADD key now member` and refresh key TTL using `PEXPIRE`
  4. Return `1` (Allowed) or `0` (Throttled)

### 2. Multi-Dimensional Keying & Limits
The rate limiter evaluates multiple dimensions to accurately classify request behavior:
- **API Key Rate Limiting**: If an API Key header is present (from a verified API partner), limits are tracked by `rate:apikey:{apiKey}:{path}` with higher limits.
- **Client Rate Limiting**: Tracked by `rate:client:{ip}:{fingerprint}:{path}` using a client fingerprint.
- **Global IP Rate Limiting**: Tracked by `rate:ip:{ip}:{path}` for the entire IP address.

### 3. Edge Case: Corporate NAT (500 Employees Behind a Single IP)
**Problem**: If we rate limit by IP address alone, a corporate office with 500 employees sharing a single NAT IP will quickly hit the rate limit and get blocked, creating false positives.
**Solution**: We implement **Multi-dimensional Fingerprinted Limits**:
1. **Fingerprint Creation**: We generate a unique browser/client fingerprint for each request by hashing key client header combinations (`User-Agent` + `Accept-Language` + `Accept-Encoding`) using SHA-256.
2. **Differentiated Limits**:
   - **Individual limit** (e.g. 60 requests/min) is checked against the combination of `IP + Fingerprint + Path`. Since different employees use different devices, browsers, and languages, they have distinct fingerprints and will not share this limit.
   - **Global IP limit** (e.g. 10x higher, like 600 requests/min) is checked against `IP + Path`. This protects our Elasticsearch/DB endpoints from bulk IP-level scraping or DDoS attacks while comfortably accommodating 500 legitimate human users browsing at a normal human pace.

### 4. Graceful Fallback Strategy (Redis Downtime)
If the Redis server becomes unavailable (connection timed out, connection refused, or network partitioning), the rate limiter:
- Automatically catches the exception and logs a warning.
- Gracefully degrades to use the **`InMemoryRateLimiter`** (via `IMemoryCache`), ensuring that API availability is never disrupted (fails safe).
- Subscribes to connection restored events to automatically resume distributed Redis rate limiting once Redis comes back online.

### 5. IP Spoofing Mitigation (Trusted Proxy Chain)
To prevent attackers from spoofing their IP address via the `X-Forwarded-For` header:
- The middleware validates the connecting IP address against a configurable list of `TrustedProxies`.
- If the immediate connecting IP is a trusted proxy, it traverses the `X-Forwarded-For` list from right to left to identify the first untrusted IP, which is treated as the client IP.
- If the immediate connecting IP is not a trusted proxy, the `X-Forwarded-For` header is completely ignored, preventing spoofed header injections.

---

## Future Improvements

- Behavioral analysis (mouse movements, keystroke patterns)
- TLS fingerprinting (JA3/JA4)
- Metrics integration (Prometheus/Grafana)
- Geo-based rules (blocking traffic from specific high-risk regions)

## Assumptions

## Running The Project

1. Run the API project:
   ```bash
   dotnet run --project BotDetector.API
   ```
2. Run the unit test suite:
   ```bash
   dotnet test
   ```

## Sample Detection Result