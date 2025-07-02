# Setting Default Branch to Dev

**Date:** July 2, 2025 07:15

## User Request
Figure out how to set the default branch to dev for the GitHub repository.

## Investigation & Actions

1. Checked if GitHub CLI is installed:
   ```
   gh version 2.74.1 (2025-06-10)
   ```

2. Verified GitHub CLI authentication status:
   ```
   ✓ Logged in to github.com account AniaPietrusewicz (keyring)
   ```

3. Attempted to set default branch using GitHub CLI:
   ```
   HTTP 403: Resource not accessible by personal access token
   ```

4. Checked current branch configuration:
   ```
   HEAD branch: main
   ```

5. Opened GitHub repository settings in browser:
   ```
   https://github.com/AniaPietrusewicz/MiniBrainDesktop/settings/branches
   ```

## Instructions to Set Default Branch

Since we can't change the default branch via command line due to permission limitations, here's how to do it through the GitHub web interface:

1. Go to: https://github.com/AniaPietrusewicz/MiniBrainDesktop/settings/branches
2. Under "Default branch" section, click the dropdown menu (currently showing "main")
3. Select "dev" from the dropdown
4. Click the "Update" button
5. In the confirmation dialog, click "I understand, update the default branch"

## Conclusion
The default branch needs to be changed manually through the GitHub web interface. The necessary steps have been provided and the browser has been opened to the correct settings page.

## Next Steps
After changing the default branch on GitHub, run `git remote update` to refresh local Git information.
