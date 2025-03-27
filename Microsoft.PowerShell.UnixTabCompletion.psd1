#
# Module manifest for module 'Microsoft.PowerShell.UnixTabCompletion'
#

@{

# Script module or binary module file associated with this manifest.
RootModule = 'Microsoft.PowerShell.UnixTabCompletion.dll'

# Version number of this module.
ModuleVersion = '0.6.0'

# Supported PSEditions
CompatiblePSEditions = 'Core'

# ID used to uniquely identify this module
GUID = '042bff5f-9644-43ef-8f4e-d8b8ed5a1f97'

# Author of this module
Author = 'Microsoft'

# Company or vendor of this module
CompanyName = 'Microsoft'

# Copyright statement for this module
Copyright = '© Microsoft'

# Description of the functionality provided by this module
Description = 'Get parameter completion for native Unix utilities. Requires zsh or bash.'

# Minimum version of the PowerShell engine required by this module
PowerShellVersion = '7.4'

# Functions to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no functions to export.
FunctionsToExport = @()

# Cmdlets to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no cmdlets to export.
CmdletsToExport = @(
    'Import-PSUnixTabCompletion',
    'Remove-PSUnixTabCompletion',
    'Set-PSUnixTabCompletion',
    'Get-PSUnixTabCompletion'
)

# Variables to export from this module
VariablesToExport = @()

# Aliases to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no aliases to export.
AliasesToExport = @()

# Private data to pass to the module specified in RootModule/ModuleToProcess. This may also contain a PSData hashtable with additional module metadata used by PowerShell.
PrivateData = @{

    PSData = @{

        # Tags applied to this module. These help with module discovery in online galleries.
        # Tags = @()

        # A URL to the license for this module.
        LicenseUri = 'https://raw.githubusercontent.com/PowerShell/UnixCompleters/master/LICENSE'

        # A URL to the main website for this project.
        ProjectUri = 'https://github.com/PowerShell/UnixCompleters'

        # A URL to an icon representing this module.
        # IconUri = ''

    } # End of PSData hashtable

} # End of PrivateData hashtable

# HelpInfo URI of this module
# HelpInfoURI = ''

}
