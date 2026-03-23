# Task 03: Optional Nix Iteration Tooling

## Status

Not started

## Goal

Try a Linux-side compile workflow only if it provides useful iteration value without becoming a maintenance burden.

## Depends On

- 01 Repo And Project Setup

## Deliverables

- optional plugin dev shell or app target
- decision record on whether Nix-side compilation is worth keeping
- explicit fallback to Windows-only plugin builds if not

## Steps

- Attempt a minimal Mono/MSBuild-based compile workflow on NixOS.
- Keep the experiment separate from the main Gumo dev shell unless the overhead is trivial.
- Record what works and what fails.
- Decide whether to keep or remove the Nix-side plugin tooling.

## Acceptance Criteria

- The repo has a clear answer on whether Nix-side plugin compilation is supported.
- Failure to support plugin compilation on NixOS does not block plugin work.
- The main project remains clean if the experiment is dropped.

## Tracking Checklist

- [ ] Minimal Linux compile path attempted
- [ ] Result documented
- [ ] Keep/drop decision made
- [ ] Main workflow unaffected by failure

## Notes

- Best-effort only. Do not overinvest here.
