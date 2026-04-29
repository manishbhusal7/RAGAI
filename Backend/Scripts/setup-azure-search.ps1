# # Azure Search Index Setup Script
# # This script creates the necessary index for the RAG application

# param(
#     [Parameter(Mandatory=$true)]
#     [string]$SearchServiceName,

#     [Parameter(Mandatory=$true)]
#     [string]$SearchApiKey,

#     [Parameter(Mandatory=$true)]
#     [string]$IndexName = "azureblob-index"
# )

# $searchEndpoint = "https://$SearchServiceName.search.windows.net"

# # Define the index schema
# $indexDefinition = @{
#     name = $IndexName
#     fields = @(
#         @{
#             name = "id"
#             type = "Edm.String"
#             key = $true
#             searchable = $false
#             filterable = $false
#             sortable = $false
#             facetable = $false
#             retrievable = $true
#         },
#         @{
#             name = "content"
#             type = "Edm.String"
#             key = $false
#             searchable = $true
#             filterable = $false
#             sortable = $false
#             facetable = $false
#             retrievable = $true
#             analyzer = "standard"
#         },
#         @{
#             name = "title"
#             type = "Edm.String"
#             key = $false
#             searchable = $true
#             filterable = $true
#             sortable = $true
#             facetable = $false
#             retrievable = $true
#             analyzer = "standard"
#         },
#         @{
#             name = "source"
#             type = "Edm.String"
#             key = $false
#             searchable = $true
#             filterable = $true
#             sortable = $true
#             facetable = $true
#             retrievable = $true
#         },
#         @{
#             name = "document_id"
#             type = "Edm.String"
#             key = $false
#             searchable = $false
#             filterable = $true
#             sortable = $false
#             facetable = $false
#             retrievable = $true
#         },
#         @{
#             name = "chunk_index"
#             type = "Edm.Int32"
#             key = $false
#             searchable = $false
#             filterable = $true
#             sortable = $true
#             facetable = $false
#             retrievable = $true
#         },
#         @{
#             name = "url"
#             type = "Edm.String"
#             key = $false
#             searchable = $false
#             filterable = $false
#             sortable = $false
#             facetable = $false
#             retrievable = $true
#         },
#         @{
#             name = "created_date"
#             type = "Edm.DateTimeOffset"
#             key = $false
#             searchable = $false
#             filterable = $true
#             sortable = $true
#             facetable = $false
#             retrievable = $true
#         },
#         @{
#             name = "last_modified"
#             type = "Edm.DateTimeOffset"
#             key = $false
#             searchable = $false
#             filterable = $true
#             sortable = $true
#             facetable = $false
#             retrievable = $true
#         }
#     )
# }

# # Convert to JSON
# $indexJson = $indexDefinition | ConvertTo-Json -Depth 10

# # Headers for the API request
# $headers = @{
#     "Content-Type" = "application/json"
#     "api-key" = $SearchApiKey
# }

# # Create the index
# $createIndexUrl = "$searchEndpoint/indexes/$IndexName?api-version=2023-11-01"

# Write-Host "Creating Azure Search index '$IndexName'..." -ForegroundColor Green

# try {
#     $response = Invoke-RestMethod -Uri $createIndexUrl -Method PUT -Headers $headers -Body $indexJson
#     Write-Host "Index '$IndexName' created successfully!" -ForegroundColor Green
#     Write-Host "Index definition:" -ForegroundColor Yellow
#     $response | ConvertTo-Json -Depth 10
# }
# catch {
#     Write-Host "Error creating index: $($_.Exception.Message)" -ForegroundColor Red
#     if ($_.Exception.Response) {
#         $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
#         $responseBody = $reader.ReadToEnd()
#         Write-Host "Response: $responseBody" -ForegroundColor Red
#     }
# }

# Write-Host "`nSetup complete!" -ForegroundColor Green
# Write-Host "You can now configure your appsettings.json with:" -ForegroundColor Yellow
# Write-Host "  - SearchServiceName: $SearchServiceName" -ForegroundColor Cyan
# Write-Host "  - IndexName: $IndexName" -ForegroundColor Cyan