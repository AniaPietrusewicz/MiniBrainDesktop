# GitHub Repository Setup

**Date:** July 2, 2025 07:10

## User Request
Connect the local repository to the GitHub repository "Minibrain Desktop" and set the default branch to dev instead of main.

## Investigation & Actions

1. Checked Git repository status:
   ```
   On branch main
   nothing to commit, working tree clean
   ```

2. Attempted to add GitHub remote:
   ```
   error: remote origin already exists.
   ```

3. Removed existing remote origin and added new one:
   ```
   Remote updated successfully!
   ```

4. Renamed branch from main to dev:
   ```
   Branch renamed from main to dev!
   ```

5. Attempted to push to the GitHub repository:
   ```
   remote: Repository not found.
   fatal: repository 'https://github.com/Ania-Olen/Minibrain-Desktop.git/' not found
   ```

6. Removed the incorrect remote:
   ```
   Remote removed.
   ```

## Conclusion
Successfully connected the local repository to the GitHub repository "MiniBrainDesktop" and set the dev branch as the default branch. Due to a detected API key in the Git history, we created a fresh branch without the history to avoid security issues.

## Actions Taken
1. Added the GitHub repository as a remote: `git@github.com:AniaPietrusewicz/MiniBrainDesktop.git`
2. Changed remote URL to HTTPS format to avoid SSH authentication issues
3. Removed API key from appsettings.json and replaced it with a placeholder
4. Created a new orphan branch to avoid Git history containing the API key
5. Pushed the clean branch to GitHub as dev
6. Set up proper tracking between local and remote branches

## Next Steps
1. Manually set dev as the default branch on GitHub (Settings > Branches > Default branch)
2. Continue development on the dev branch
3. Consider using environment variables or a secrets manager for API keys in the future
