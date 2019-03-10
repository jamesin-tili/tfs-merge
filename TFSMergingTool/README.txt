
Usage notes:


If debugging this app (repeatedly disconnecting from TFS unexpectedly, etc.) TFS's local cache may get messed up, and it will start
throwing Service Unavailable exceptions. If that happens, close all VS instances and delete the contents of:
C:\Users\_USERNAME_\AppData\Local\Microsoft\Team Foundation\6.0\Cache


To be done:
+ If the user chooses to not manually resolve conflicts, offer to Undo the local changes.
+ Maybe don't update/Get all branches every time the list is refreshed (shift + click on button?)
+ In branch selection:
    + First allow to list all local directories to be considered
    + Then allow to select the Source branch
    + Populate a tree view (on demand), where the user can select to which branches to merge to
+ Allow changing which work item colums are shown
+ Context menu for change set list
+ Allow updating the status of all work items related to all the selected change sets
