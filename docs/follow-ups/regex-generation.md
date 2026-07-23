# Regex generation

Status: open. Priority: low. Depends on assembly/API and AOT policy.

Evaluate `[GeneratedRegex]` or a project-owned source generator against the
current `RegexOptions.Compiled` runtime baseline. Preserve constructible type
names, patterns, declared options, visibility, timeout behavior, and culture.

Done when compatibility and performance are measured and the chosen approach
has deterministic verification.
