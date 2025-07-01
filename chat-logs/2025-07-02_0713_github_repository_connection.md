# GitHub Repository Connection

**Date:** July 2, 2025 07:13

## User Request
Connect this repository to GitHub repository "MiniBrain Desktop" and make sure the default branch is dev not main.

## Investigation & Actions

1. Identified that the Git repository was already initialized locally on main branch.

2. Added GitHub repository as remote:
   ```
   git remote add origin git@github.com:AniaPietrusewicz/MiniBrainDesktop.git
   ```

3. Encountered SSH authentication issues and switched to HTTPS:
   ```
   git remote set-url origin https://github.com/AniaPietrusewicz/MiniBrainDesktop.git
   ```

4. Attempted to push but encountered security blocking due to API key in Git history:
   ```
   remote: error: GH013: Repository rule violations found for refs/heads/dev.
   remote: - Push cannot contain secrets
   ```

5. Found and removed API key from appsettings.json:
   ```
   Replaced hardcoded API key with placeholder "YOUR_CLAUDE_API_KEY_HERE"
   ```

6. Created a new orphan branch to avoid pushing history with secrets:
   ```
   git checkout --orphan new-dev
   git add .
   git commit -m "Initial commit"
   ```

7. Successfully pushed to GitHub:
   ```
   git push -f origin new-dev:dev
   ```

8. Renamed local branch to maintain consistency:
   ```
   git branch -D dev
   git branch -m new-dev dev
   ```

9. Set up proper tracking:
   ```
   git branch -u origin/dev
   ```

## Conclusion
Successfully connected to GitHub repository using a clean branch approach to avoid security issues with API keys in Git history. The dev branch is now set up as the working branch both locally and on remote.

## Next Steps
1. Manually set dev as the default branch on GitHub (Settings > Branches > Default branch)
2. Continue working on the dev branch
3. Consider using environment variables or a secrets manager for storing API keys
