# Code signing policy

This page says how Costas releases are built and signed, who can sign them, and what the program sends over the
network. It applies to every file published on the
[Releases page](https://github.com/somedudenamedtony/costas/releases).

## Current status

Releases are **not code-signed yet**, so Windows SmartScreen may warn about the installer (see [Install](install.md)).
Signing is wired into the release build and turns on as soon as a signing certificate is in place. Once it is, this
page will name the certificate provider, and the installer will name its publisher.

Each release has a `.sha256` file next to every download, so you can check that the file you have is the one the
build produced.

## How releases are built

- Every release is built by [GitHub Actions](https://github.com/somedudenamedtony/costas/actions/workflows/ci.yml)
  from the public source in [somedudenamedtony/costas](https://github.com/somedudenamedtony/costas), on GitHub's
  hosted runners. Nothing is built or signed on a personal computer.
- A release is only made from the `main` branch, after the build and tests pass, and only when the maintainer starts
  it by hand (**Actions › CI › Run workflow**). Pull request builds are never released or signed.
- Signing happens inside that release run. The signing credentials are stored as GitHub repository secrets, are
  available only to release runs on `main`, and are never written to the build logs.

## What gets signed

- Costas's own programs and libraries, the installer and the uninstaller.
- The installer also carries the WSJT-X `jt9` decoder and Hamlib's `rigctld`, unmodified from those projects' own
  releases (fetched by `third_party/fetch.ps1` during the build, with their licences). Files that already
  carry a valid signature, such as the .NET runtime, are left as they are.
- Signing is checked after it runs: if any file is not validly signed, the release fails and nothing is published.

## Who can sign

Costas is published by Ember Ridge Development.

| Role | Who |
| --- | --- |
| Author, committer and reviewer | Tony, W7LIT ([somedudenamedtony](https://github.com/somedudenamedtony)) |
| Release approver | Tony, W7LIT, for Ember Ridge Development |

Changes from anyone else come in as pull requests and are reviewed before they are merged. Only the maintainer can
merge to `main` and start a release.

## Privacy

Costas does not send anything about you anywhere unless a feature you set up needs it:

- **Services you turn on**: QRZ, LoTW, eQSL, Club Log, PSK Reporter, the Reverse Beacon Network and POTA spots are
  contacted only when you set them up in Settings, and only with what that service needs (your callsign and log
  entries for the logbooks, for example).
- **Updates**: Costas asks GitHub for the latest release when it starts and once a day. You can turn this off in
  **Settings › Updates**.
- **Usage statistics**: anonymous counts of which features are used, sent to Aptabase. They never include your
  callsign, grid, frequencies, the stations you work or anything from your log, and nothing is sent while you are
  offline. [Settings › Privacy](settings.md#privacy) lists exactly what is sent and has the switch to turn it off.

Your QRZ API key and password are kept in the Windows Credential Manager, never in a file. See
[Install](install.md#where-your-data-lives) for where your log and settings are stored.

## Reporting a problem

If you think a release has been tampered with, or you find a security problem, please open an issue on
[GitHub](https://github.com/somedudenamedtony/costas/issues) or contact the maintainer through the GitHub profile
above.
