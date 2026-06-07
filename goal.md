# Goal
The purpose of this project is to help clean copilot sessions. There are lots of sessions that get created with automation, and I want to clean them up otherwise copilot tooling gets overloaded with too many files and crashes.

## Details
1. the default place for copilot working session data is "~/.copilot/session-state"
1. I want to default move the sessions I don't care about to "~/.copilot/old_session-state"
1. I want the code to be a c# local app with UI
1. I to present the data in  vscode.metadata.json & workspace.yaml to help decide what to move
1. I want the columns to  sortable, I want to rearrange the order of the columsn by drag and drop, I want the sorting to be applied from left to right for the columns, 
1. I want the ability to aggregate and unaggregate the values in the column that are the same, so I can quickly bulk apply actions to multiple groups
1. I want checkboxes next to each row
1. I want a button for move, and delete for the sessions states, delete should ask for confirmation.




## Investigations

I want to understand if I should just use file parsing or use the sdk for this activity.


### GitHub Copilot SDK
- [GitHub Copilot SDK repository](https://github.com/github/copilot-sdk)
- [Copilot SDK .NET README](https://github.com/github/copilot-sdk/blob/main/dotnet/README.md)
- [GitHub.Copilot.SDK on NuGet](https://www.nuget.org/packages/GitHub.Copilot.SDK)