# ImAI monorepo

- `Frontend/`: Angular application. See [frontend instructions](Frontend/README.md).
- `Backend/`: .NET 10 API solution. See [backend instructions](Backend/README.md).

## Continuous integration

[GitHub Actions workflow](.github/workflows/ci.yml) runs on every push to every branch (including `main`), every pull request, and manual dispatch. There are no path filters: both applications are checked on every run.

Two independent jobs run in parallel on Ubuntu:

| Check                    | Runtime     | Validation                                                                  |
| ------------------------ | ----------- | --------------------------------------------------------------------------- |
| Frontend build and tests | Node.js 24  | Clean installation with `npm ci`, production build, non-watch Angular tests |
| Backend build and tests  | .NET 10 SDK | Restore, Release build, HTTP integration tests                              |

Backend TRX test results are retained as workflow artifacts for 14 days, including on test failure. No Azure credentials or database secrets are required for Phase 2. The workflow has read-only repository permissions and does not deploy or push commits.

Pushes to `main` run these checks after the push. To require passing checks before changes reach `main`, configure a GitHub ruleset requiring pull requests and both job checks.

For a newly created GitHub monorepo, commit the root `.github/` directory alongside `Frontend/` and `Backend/`. The workflow becomes active once it is pushed to GitHub with Actions enabled.
