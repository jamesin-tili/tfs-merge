# tfs-merge
A standalone application for quick TFS merging.

If you've got multiple commits to merge from a branch to another, then using Visual Studio's built-in tools can be a pain. This app was built to make that easier.

Features:
    + Can select a changeset and merge it in one commit.
    + Can select multiple changesets and loop through them, creating a new commit for each merge.
    + Can select multiple consecutive changesets and combine them into a single merge commit.
    + Can merge from a source branch to a single target branch, or can select multiple target branches to create a chain of merges:
        Branch1 > Branch2
        Branch2 > Branch3
        ...
    + Can select commits to merge either from the source branch's commit history (fast), or from merge candidates to the second branch (slow).
    + Automatically associates the merge commit(s) to TFS work items.
    + Automatically copies the commit comment, and prefixes it with merge data in the format:
    "Branch1 > Branch2, Axel F.: <original comment>"
    These tags are automatically updated when doing a chain of merges (or continuing a chain later on), i.e., the above will become "Branch2 > Branch3, Axel F.: <original comment>".
    + Automatically commits the changes (optional).
    + For merge conflict resolution, uses tf.exe to launch Visual Studio's merge conflict resolution tools and shows a popup where the user can either try to commit again or stop the process.
    + Shows linked TFS WorkItem information in the UI and can open the selected item in a web browser.
    + Allows filtering the comment and developer name fields of the change set list, while always including current selections.
    + Discard, force, and baseless merge support.
    
Usage notes:
    + Always test the tool in a separate test project or a non-critical branch first. Start with the automic commit option disabled.
    + Uncheck the automatic commit option to customize your changes before committing them manually.
    + It is strongly advised to use a dedicated TFS workspace for merging with this tool. This is due to two reason:
        1) It makes merging *much* faster for large workspaces (can use a server workspace, which also requires significantly less disk space than a local workspace)
        2) It is much safer. This tool does not keep track of "exluded" changes or what change is new or was there already. It will perform the merge operation from source to target branch, then commit everything in the target branch. Using a dedicated workspace means you should not have any pending development changes in the target branch.
    + Process for merge conflict resolution:
        1) Start the merging process. If a conflict arises, a popup dialog will allow trying again, launching Visual Studio to resolve the conflict, or stopping the process.
        2) Resolve the conflict in Visual Studio and save the changes to the target file.
        3a) In this tool's popup dialog, click to try again. The merge is completed.
        3b) If you decided to stop the process, then note that the conflicted items are still in that state in your workspace. Undo those pending changes in Visual Studio before doing any further merging.
        
Pending developments:
    + Make TFS workitem data retrieval asynchronous, canceling it when necessary.
    + Look for any existing pending changes in the target workspace before performing a merge. Should at least warn the user if any are found. Note that existing conflicts are checked already.
    + (Track changed files and handle exluded changes)
    