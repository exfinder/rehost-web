---
status: accepted
---

# Use a portable configuration baseline

The host supplies portable root configuration defaults, including settings that
disable unsupported subsystems. Existing `System.Configuration` parsing,
inheritance, and validation determine the effective application configuration.

Bootstrap must not silently rewrite application configuration. An effective
setting that requests unsupported behavior or conflicts with host-owned policy
fails where the retained initialization sequence normally consumes that
section, identifying the exact section and property.
