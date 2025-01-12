function CreateDraftRelease {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$tag,
    [Parameter()][string]$releaseFiles
  )

  $match = [regex]::Match($tag, '^(.*?-)(.*)$')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse prefix or version from tag'
  }

  $tagPrefix = $match.Groups[1].Value
  $version = $match.Groups[2].Value

  $projects = @(Get-ChildItem -Path src/**/*.csproj | Select-String "<MinVerTagPrefix>$tagPrefix</MinVerTagPrefix>" -List | Select Path)

  if ($projects.Length -eq 0)
  {
      throw 'No projects found with MinVerTagPrefix matching prefix from tag'
  }

  $notes = ''

  foreach ($project in $projects)
  {
      $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project.Path)

      $changelogContent = Get-Content -Path "src/$projectName/CHANGELOG.md"

      $started = $false
      $content = ""

      foreach ($line in $changelogContent)
      {
          if ($line -like "## $version" -and $started -ne $true)
          {
              $started = $true
          }
          elseif ($line -like "Released *" -and $started -eq $true)
          {
              continue
          }
          elseif ($line -like "## *" -and $started -eq $true)
          {
              break
          }
          else
          {
              if ($started -eq $true -and ([string]::IsNullOrWhitespace($line) -eq $false -or $content.Length -gt 0))
              {
                  $content += "  " + $line + "`r`n"
              }
          }
      }

      if ([string]::IsNullOrWhitespace($content) -eq $true)
      {
          $content = "   No notable changes."
      }

      $content = $content.trimend()

      $notes +=
@"
* NuGet: [$projectName v$version](https://www.nuget.org/packages/$projectName/$version)

$content

  See [CHANGELOG](https://github.com/$gitRepository/blob/$tag/src/$projectName/CHANGELOG.md) for details.

"@
  }

  if ($version -match '-alpha' -or $version -match '-beta' -or $version -match '-rc')
  {
    gh release create $tag $releaseFiles `
      --title $tag `
      --verify-tag `
      --notes $notes `
      --prerelease `
      --draft
  }
  else
  {
    gh release create $tag $releaseFiles `
      --title $tag `
      --verify-tag `
      --notes $notes `
      --latest `
      --draft
  }
}

Export-ModuleMember -Function CreateDraftRelease

function TryPostPackagesReadyNoticeOnPrepareReleasePullRequest {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$tag,
    [Parameter(Mandatory=$true)][string]$tagSha,
    [Parameter(Mandatory=$true)][string]$packagesUrl,
    [Parameter(Mandatory=$true)][string]$botUserName
  )

  $prListResponse = gh pr list --search $tagSha --state merged --json number,author,title,comments | ConvertFrom-Json

  if ($prListResponse.Length -eq 0)
  {
    Write-Host 'No prepare release PR found for tag & commit skipping post notice'
    return
  }

  foreach ($pr in $prListResponse)
  {
    if ($pr.author.login -ne $botUserName -or $pr.title -ne "[repo] Prepare release $tag")
    {
      continue
    }

    $foundComment = $false
    foreach ($comment in $pr.comments)
    {
      if ($comment.author.login -eq $botUserName -and $comment.body.StartsWith("I just pushed the [$tag]"))
      {
        $foundComment = $true
        break
      }
    }

    if ($foundComment -eq $false)
    {
      continue
    }

  $body =
@"
The packages for [$tag](https://github.com/$gitRepository/releases/tag/$tag) are now available: $packagesUrl.

Once these packages have been validated have a maintainer post a comment with "/PushPackages" in the body if you would like me to push to NuGet.
"@

    $pullRequestNumber = $pr.number

    gh pr comment $pullRequestNumber --body $body
    return
  }

  Write-Host 'No prepare release PR found matched author and title with a valid comment'
}

Export-ModuleMember -Function TryPostPackagesReadyNoticeOnPrepareReleasePullRequest

function PushPackagesPublishReleaseUnlockAndPostNoticeOnPrepareReleasePullRequest {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$pullRequestNumber,
    [Parameter(Mandatory=$true)][string]$botUserName,
    [Parameter(Mandatory=$true)][string]$commentUserName,
    [Parameter(Mandatory=$true)][string]$artifactDownloadPath,
    [Parameter(Mandatory=$true)][string]$pushToNuget
  )

  $prViewResponse = gh pr view $pullRequestNumber --json author,title,comments | ConvertFrom-Json

  if ($prViewResponse.author.login -ne $botUserName)
  {
      throw 'PR author was unexpected'
  }

  $match = [regex]::Match($prViewResponse.title, '^\[repo\] Prepare release (.*)$')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse tag from PR title'
  }

  $tag = $match.Groups[1].Value

  $commentUserPermission = gh api "repos/$gitRepository/collaborators/$commentUserName/permission" | ConvertFrom-Json
  if ($commentUserPermission.permission -ne 'admin')
  {
    gh pr comment $pullRequestNumber `
      --body "I'm sorry @$commentUserName but you don't have permission to push packages. Only maintainers can push to NuGet."
    return
  }

  $foundComment = $false
  $packagesUrl = ''
  foreach ($comment in $prViewResponse.comments)
  {
    if ($comment.author.login -eq $botUserName -and $comment.body.StartsWith("The packages for [$tag](https://github.com/$gitRepository/releases/tag/$tag) are now available:"))
    {
      $foundComment = $true
      break
    }
  }

  if ($foundComment -eq $false)
  {
    throw 'Could not find package push comment on pr'
  }

  gh release download $tag `
    -p "$tag-packages.zip" `
    -D "$artifactDownloadPath"

  Expand-Archive -LiteralPath "$artifactDownloadPath/$tag-packages.zip" -DestinationPath "$artifactDownloadPath\"

  if ($pushToNuget -eq 'true')
  {
    gh pr comment $pullRequestNumber `
      --body "I am uploading the packages for ``$tag`` to NuGet and then I will publish the release."

    nuget push "$artifactDownloadPath/**/*.nupkg" -Source https://api.nuget.org/v3/index.json -ApiKey "$env:NUGET_TOKEN" -SymbolApiKey "$env:NUGET_TOKEN"

    if ($LASTEXITCODE -gt 0)
    {
      gh pr comment $pullRequestNumber `
        --body "Something went wrong uploading the packages for ``$tag`` to NuGet."

      throw 'nuget push failure'
    }
  }
  else {
    gh pr comment $pullRequestNumber `
      --body "I am publishing the release without uploading the packages to NuGet because a token wasn't configured."
  }

  gh release edit $tag --draft=false

  gh pr unlock $pullRequestNumber
}

Export-ModuleMember -Function PushPackagesPublishReleaseUnlockAndPostNoticeOnPrepareReleasePullRequest

function CreateStableVersionUpdatePullRequest {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$tag,
    [Parameter()][string]$targetBranch="main",
    [Parameter()][string]$gitUserName,
    [Parameter()][string]$gitUserEmail
  )

  $match = [regex]::Match($tag, '.*?-(.*)')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse version from tag'
  }

  $packageVersion = $match.Groups[1].Value

  $branch="release/post-stable-${tag}-update"

  if ([string]::IsNullOrEmpty($gitUserName) -eq $false)
  {
    git config user.name $gitUserName
  }
  if ([string]::IsNullOrEmpty($gitUserEmail) -eq $false)
  {
    git config user.email $gitUserEmail
  }

  git switch --create $branch origin/$targetBranch --no-track 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git switch failure'
  }

  (Get-Content Directory.Packages.props) `
      -replace '<OTelLatestStableVer>.*<\/OTelLatestStableVer>', "<OTelLatestStableVer>$packageVersion</OTelLatestStableVer>" |
    Set-Content Directory.Packages.props

  git add Directory.Packages.props 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git add failure'
  }

  git commit -m "Update OTelLatestStableVer in Directory.Packages.props to $packageVersion." 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git commit failure'
  }

  git push -u origin $branch 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git push failure'
  }

  $body =
@"
Note: This PR was opened automatically by the [post-release workflow](https://github.com/$gitRepository/actions/workflows/post-release.yml).

Merge once packages are available on NuGet and the build passes.

## Changes

* Sets ``OTelLatestStableVer`` in ``Directory.Packages.props`` to ``$packageVersion``.
"@

  gh pr create `
    --title "[repo] Core stable release $packageVersion updates" `
    --body $body `
    --base $targetBranch `
    --head $branch `
    --label infra
}

Export-ModuleMember -Function CreateStableVersionUpdatePullRequest

function InvokeCoreVersionUpdateWorkflowInRemoteRepository {
  param(
    [Parameter(Mandatory=$true)][string]$remoteGitRepository,
    [Parameter(Mandatory=$true)][string]$tag,
    [Parameter()][string]$targetBranch="main"
  )

  $match = [regex]::Match($tag, '^(.*?-)(.*)$')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse prefix or version from tag'
  }

  gh workflow run "core-version-update.yml" `
    --repo $remoteGitRepository `
    --ref $targetBranch `
    --field "tag=$tag"
}

Export-ModuleMember -Function InvokeCoreVersionUpdateWorkflowInRemoteRepository

function TryPostReleasePublishedNoticeOnPrepareReleasePullRequest {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$botUserName,
    [Parameter(Mandatory=$true)][string]$tag
  )

  $tagSha = git rev-list -n 1 $tag 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git rev-list failure'
  }

  $prListResponse = gh pr list --search $tagSha --state merged --json number,author,title,comments | ConvertFrom-Json

  if ($prListResponse.Length -eq 0)
  {
    Write-Host 'No prepare release PR found for tag & commit skipping post notice'
    return
  }

  foreach ($pr in $prListResponse)
  {
    if ($pr.author.login -ne $botUserName -or $pr.title -ne "[repo] Prepare release $tag")
    {
      continue
    }

    $foundComment = $false
    foreach ($comment in $pr.comments)
    {
      if ($comment.author.login -eq $botUserName -and $comment.body.StartsWith("The packages for [$tag](https://github.com/$gitRepository/releases/tag/$tag) are now available:"))
      {
        $foundComment = $true
        break
      }
    }

    if ($foundComment -eq $false)
    {
      continue
    }

  $body =
@"
The release [$tag](https://github.com/$gitRepository/releases/tag/$tag) has been published and packages should be available on NuGet momentarily.

Have a nice day!
"@

    $pullRequestNumber = $pr.number

    gh pr comment $pullRequestNumber --body $body
    return
  }

  Write-Host 'No prepare release PR found matched author and title with a valid comment'
}

Export-ModuleMember -Function TryPostReleasePublishedNoticeOnPrepareReleasePullRequest
