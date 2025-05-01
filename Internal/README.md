# Repositories

- Public repository: https://github.com/Unity-Technologies/com.unity.ide.visualstudio is a mirror of the internal repository 
- Internal repository: https://github.cds.internal.unity3d.com/unity/com.unity.ide.visualstudio

# How to contribute (for everyone)

- Please create a PR against the latest `version-x.y.z` branch.
- In the [`.scripts`](.scripts) folder, you should find how to build IDE integrations with VS and VS for Mac. Make sure to remove all build artifacts if needed, given we do not want Unity to import unwanted files. You can find as well extra notes regarding building the Windows specific part [here](https://github.cds.internal.unity3d.com/unity/com.unity.ide.visualstudio/blob/next/master/Packages/com.unity.ide.visualstudio/Editor/COMIntegration/COMIntegration~/howtobuild.txt).
- Please add Microsoft folks on all PRs.

# How to publish new version (for Microsoft)

All you have to do is create a PR that targets *next/master*. The important part is to make the PR title the version number i.e. `2.0.3` or the target version. Also to add the changelog to the description of the PR. Then add the unity employee as a reviewer. Once we have reviewed it, and all tests are passing we will merge it to next/master (a real merge commit must be used, and not a squash!).

`next/master` is a CI branch that will update the source code as following:

- Update the package.json files with the PR title. I.e. `2.0.3` or the target version. This has to obide by the SEMVER rules for major, minor, and patch versions. The CI will otherwise fail to validate the new version.
- Update the changelog of packages/com.unity.ide.visualstudio/ with the content of the PR description.

The changelog will get the date stamp of when the PR was merged to next/master followed by the description.

When the files has been modified the branch is then merged to publish-release. Here we do a full test for all platforms (Windows, OSx, and Linux), as well as all supported version of Unity from first release to current trunk (newest alpha/beta).

If all test pass, the package will be published to an internal repo we call candidates. Here the package is accessible for all Unity developers behind our VPN.

The next step is that we merge it to the master branch. Here the CI will create a PR targeting our internal repo. This is done in order to make the new version a verified package. Verified means that it is shipped with Unity, and subjugated to our QA department and our internal test suite.
