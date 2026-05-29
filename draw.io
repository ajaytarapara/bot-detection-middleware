+------------------+
|  Client Request  |
+------------------+
          |
          v
+---------------------------+
| BotDetectionMiddleware    |
+---------------------------+
          |
          v
+---------------------------+
| TrafficClassifier         |
+---------------------------+
          |
          v
+---------------------------+
| DetectionEngine           |
+---------------------------+
          |
    +-----+-----+-----+
    |           |     |
    v           v     v
+-----------+ +-------------+ +-------------+
|UserAgent  | |Header       | |RateLimit    |
|Rule       | |AnalysisRule | |Rule         |
+-----------+ +-------------+ +-------------+
          |
          v
+---------------------------+
| Score Calculation         |
+---------------------------+
          |
          v
+---------------------------+
| Threshold Evaluation      |
+---------------------------+
          |
          v
+--------------------------------------+
| Allow | Shadow | Challenge | Block   |
+--------------------------------------+
          |
          v
+---------------------------+
| API Controller            |
+---------------------------+
          |
          v
+---------------------------+
| HTTP Response             |
+---------------------------+

===================================================================================================
Request Flow

Client
  |
  v
Middleware
  |
  +--> Internal Service ? ----> Bypass
  |
  +--> Known Abuser ? --------> Block
  |
  +--> Detection Engine
           |
           +--> UserAgentRule
           +--> HeaderAnalysisRule
           +--> RateLimitRule
           |
           v
        Score
           |
           v
        Action
           |
           +--> Allow
           +--> Shadow
           +--> Challenge
           +--> Throttle
           +--> Block