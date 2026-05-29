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

## Future Improvements

- Redis distributed rate limiting
- Sliding window rate limiting
- Behavioral analysis
- TLS fingerprinting (JA3/JA4)
- Metrics integration
- Geo-based rules

## Assumptions

## Running The Project

## Sample Detection Result