# Code comments

DEFAULT: NO COMMENTS!

A comment exists only to stop the next reader from making a mistake — deleting
a load-bearing line, "fixing" deliberate behavior.

Findings, justifications, and context from the current task go in the commit
message or docs, never in code. Never write a comment that argues the change is
correct — that is PR-description content.

Don't explain WHAT the code does, and don't reference the current task, fix, or
callers ("used by X", "added for the Y flow").

Before committing, list every comment line the diff adds; each either names a
hidden constraint or gets deleted.
