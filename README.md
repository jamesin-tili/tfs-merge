# tfs-merge
A standalone application for quick TFS merging.

If you've got multiple commits to merge from a branch to another, then using Visual Studio's built-in tools can be a pain. This app was built to make that easier.

Features:
<ul>
    <li>Can select a changeset and merge it in one commit.</li>
    <li>Can select multiple changesets and loop through them, creating a new commit for each merge.</li>
    <li>Can select multiple consecutive changesets and combine them into a single merge commit.</i>
    <li>Can merge from a source branch to a single target branch, or can select multiple target branches to create a chain of merges:<br>
        Branch1 > Branch2<br>
        Branch2 > Branch3<br>
        ...</i>
    <li>Can select commits to merge either from the source branch's commit history (fast), or from merge candidates to the second branch (slow).</i>
    <li>Automatically associates the merge commit(s) to TFS work items.</i>
    <li>Automatically copies the commit comment, and prefixes it with merge data in the format:<br>
    "Branch1 > Branch2, Axel F.: <original comment>"<br>
    These tags are automatically updated when doing a chain of merges (or continuing a chain later on), i.e., the above will become:<br> "Branch2 > Branch3, Axel F.: <original comment>".</i>
    <li>Automatically commits the changes (optional).</li>
    <li>For merge conflict resolution, uses tf.exe to launch Visual Studio's merge conflict resolution tools and shows a popup where the user can either try to commit again or stop the process.</li>
    <li>Shows linked TFS WorkItem information in the UI and can open the selected item in a web browser.</li>
    <li>Allows filtering the comment and developer name fields of the change set list, while always including current selections.</li>
    <li>Discard, force, and baseless merge support.</li>
</ul>
    
Usage notes:
<ul>
    <li><strong>Always test the tool in a separate test project or a non-critical branch first.</strong> Start with the automic commit option disabled.</li>
    <li>Uncheck the automatic commit option to customize your changes before committing them manually.</li>
    <li>
    <strong>It is strongly advised to use a dedicated TFS workspace for merging with this tool.</strong> This is due to two reason:
        <ol>
        <li>It makes merging *much* faster for large workspaces (can use a server workspace, which also requires significantly less disk space than a local workspace)
        <li>It is much safer. This tool does not keep track of "exluded" changes or what change is new or was there already. It will perform the merge operation from source to target branch, then commit everything in the target branch. Using a dedicated workspace means you should not have any pending development changes in the target branch.
            </ol>
    </li>
    <li>
        Process for merge conflict resolution:
        <ol>
        <li>Start the merging process. If a conflict arises, a popup dialog will allow trying again, launching Visual Studio to resolve the conflict, or stopping the process.
        <li>Resolve the conflict in Visual Studio and save the changes to the target file.
        <li>In this tool's popup dialog, click to try again. The merge is completed.
        <li>If you decided to stop the process, then note that the conflicted items are still in that state in your workspace. Undo those pending changes in Visual Studio before doing any further merging.
        </ol>
    </li>
</ul>
        
Pending developments:
<ul>
    <li>Make TFS workitem data retrieval asynchronous, canceling it when necessary.
    <li>Look for any existing pending changes in the target workspace before performing a merge. Should at least warn the user if any are found. Note that existing conflicts are checked already.
    <li>(Track changed files and handle exluded changes)
</ul>
