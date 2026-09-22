# Legal and licensing notes

## Project license

DevAtlas is distributed under the MIT License. The complete license text is in [`LICENSE`](LICENSE), including the copyright notice and warranty disclaimer.

The MIT license permits use, modification, distribution, and sublicensing provided that the copyright and permission notices are retained. It does not provide a warranty or imply that the software is suitable for a particular purpose.

## Third-party software

The application currently depends on open-source .NET and Avalonia packages. Before publishing a release artifact, maintainers must generate and review a dependency inventory containing:

- package name and exact version
- license identifier and license text location
- direct or transitive dependency status
- required notices or attribution
- known security advisories

Third-party license notices must ship with packaged distributions where their licenses require it. The dependency inventory should be generated from the resolved lock/assets information, not from a manually maintained list.

## User content and privacy

DevAtlas is designed to keep project metadata, Git information, command history, and activity history on the user's device. It must not upload repository contents or require a user account. Git network operations are user-initiated workflow operations, not application telemetry.

The application may read files, process information, and Git metadata inside workspace roots selected by the user. The UI and documentation must make that scope clear. The application must not claim to provide legal, compliance, licensing, or security advice.

## Release responsibility

Maintainers are responsible for reviewing dependency licenses, platform redistribution requirements, installer terms, trademarks, and applicable privacy obligations before each public release. This document is an engineering record, not legal advice.
