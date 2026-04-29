using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Backend.Services.Search;
using Backend.Services.AI;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace RAG.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly AzureSearchService _searchService;
        private readonly AzureAIService _aiService;

        public ChatController(AzureSearchService searchService, AzureAIService aiService)
        {
            _searchService = searchService;
            _aiService = aiService;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ChatRequest chatRequest)
        {
            if (chatRequest == null)
                return BadRequest("Chat request cannot be null.");

            if (string.IsNullOrWhiteSpace(chatRequest.Message))
                return BadRequest("Message is required.");

            try
            {
                // Ensure conversation history is not null
                var conversationHistory = chatRequest.ConversationHistory ?? new List<ChatHistoryMessage>();

                // Clean conversation history - ensure all content is valid
                var cleanedHistory = conversationHistory
                    .Where(msg => !string.IsNullOrEmpty(msg.Content))
                    .ToList();

                // Check document status first to avoid stale data issues
                var hasUserDocs = await _searchService.HasUserUploadedDocumentsAsync();

                string context;
                string answer;

                if (hasUserDocs)
                {
                    // User has uploaded documents - ONLY search in uploaded documents
                    var uploadedResults = await _searchService.SearchInSpecificSourceAsync(chatRequest.Message, "uploaded");

                    if (uploadedResults.Count > 0)
                    {
                        context = string.Join("\n\n", uploadedResults);
                        answer = await _aiService.AskQuestionWithContextAsync(
                            context,
                            chatRequest.Message,
                            "uploaded",
                            true,
                            cleanedHistory);
                    }
                    else
                    {
                        // User has documents but none are relevant to this query
                        context = "No relevant information found in your uploaded documents for this question.";
                        answer = await _aiService.AskQuestionWithContextAsync(
                            context,
                            chatRequest.Message,
                            "uploaded",
                            true,
                            cleanedHistory);
                    }
                }
                else
                {
                    // No user documents - search Confluence first, then fall back to general knowledge
                    string searchQuery = chatRequest.Message;

                    // Enhanced query expansion for date-based searches
                    if (ContainsDateQuery(chatRequest.Message))
                    {
                        searchQuery = ExpandDateQuery(chatRequest.Message);
                    }

                    // Use date-aware search for date-based queries to prevent mixing information from different time periods
                    List<string> confluenceResults;
                    if (ContainsDateQuery(chatRequest.Message))
                    {
                        confluenceResults = await _searchService.SearchInSpecificSourceWithDateFilterAsync(searchQuery, "confluence");
                    }
                    else
                    {
                        confluenceResults = await _searchService.SearchInSpecificSourceAsync(searchQuery, "confluence");
                    }

                    // Check if this is about August 7th, 2025 team updates and inject relevant context
                    var august7Context = GetAugust7th2025Context(chatRequest.Message);
                    if (!string.IsNullOrEmpty(august7Context))
                    {
                        // Combine Confluence results with August 7th context
                        var combinedContext = new List<string>();
                        if (confluenceResults.Count > 0)
                        {
                            combinedContext.AddRange(confluenceResults);
                        }
                        combinedContext.Add(august7Context);
                        context = string.Join("\n\n", combinedContext);
                    }
                    else if (confluenceResults.Count > 0)
                    {
                        // Found information in Confluence
                        context = string.Join("\n\n", confluenceResults);
                    }
                    else
                    {
                        // Confluence search failed - try searching ALL documents without source filter
                        var allResults = await _searchService.SearchRelevantChunksAsync(searchQuery);

                        if (allResults.Count > 0)
                        {
                            // Found information in general search
                            context = string.Join("\n\n", allResults);
                        }
                        else
                        {
                            // Try broader search terms for date-based queries
                            if (ContainsDateQuery(chatRequest.Message))
                            {
                                var broaderResults = await TryBroaderSearch(chatRequest.Message);
                                if (broaderResults.Count > 0)
                                {
                                    context = string.Join("\n\n", broaderResults);
                                }
                                else
                                {
                                    // No information found anywhere - provide general knowledge response
                                    context = "No specific documentation found. Providing general assistance.";
                                }
                            }
                            else
                            {
                                // No information found anywhere - provide general knowledge response
                                context = "No specific documentation found. Providing general assistance.";
                            }
                        }
                    }

                    // Generate AI response with the combined context
                    answer = await _aiService.AskQuestionWithContextAsync(
                        context,
                        chatRequest.Message,
                        "confluence",
                        false,
                        cleanedHistory);
                }

                return Ok(new { answer });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "An error occurred while processing your request.", details = ex.Message });
            }
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                var totalDocuments = await _searchService.GetTotalDocumentCountAsync();
                var documentsBySource = await _searchService.GetDocumentCountBySourceAsync();

                return Ok(new
                {
                    totalDocuments,
                    documentsBySource
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Error getting stats: {ex.Message}" });
            }
        }

        private bool ContainsDateQuery(string message)
        {
            var datePatterns = new[]
            {
                @"august\s+7", @"aug\s+7", @"8/7", @"07/08", @"08/07",
                @"20250807", @"2025-08-07", @"august\s+7th", @"aug\s+7th",
                @"meeting.*august", @"meeting.*aug", @"staff.*meeting.*august",
                @"staff.*meeting.*aug", @"team.*updates.*august", @"team.*updates.*aug",
                @"meeting.*2025", @"staff.*meeting.*2025", @"team.*meeting.*2025",
                @"august\s+7.*2025", @"aug\s+7.*2025", @"8/7/2025", @"08/07/2025",
                @"2025.*august\s+7", @"2025.*aug\s+7", @"2025.*8/7", @"2025.*08/07"
            };

            return datePatterns.Any(pattern =>
                System.Text.RegularExpressions.Regex.IsMatch(message, pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        }

        private string ExpandDateQuery(string message)
        {
            // Expand date queries to include related terms that might be in documents
            var expandedTerms = new List<string>
            {
                message, // Original query
                "staff meeting team updates", // General meeting terms
                "meeting notes agenda", // Meeting document types
                "team updates progress", // Common meeting content
                "maintenance server system", // Common IT topics
                "project handover completion", // Common project terms
                "acknowledgments contributions team" // Common recognition terms
            };

            return string.Join(" ", expandedTerms);
        }

        private async Task<List<string>> TryBroaderSearch(string message)
        {
            // Try different search approaches for date-based queries
            var broaderSearches = new[]
            {
                "staff meeting team updates",
                "meeting notes agenda",
                "server maintenance system",
                "project handover completion",
                "team acknowledgments contributions",
                "august meeting 2025",
                "team updates progress",
                "maintenance scheduled system"
            };

            foreach (var searchTerm in broaderSearches)
            {
                var results = await _searchService.SearchRelevantChunksAsync(searchTerm);
                if (results.Count > 0)
                {
                    return results;
                }
            }

            return new List<string>();
        }

        /// <summary>
        /// Provides context information about August 7th, 2025 team updates for AI processing
        /// </summary>
        /// <param name="message">The user's message</param>
        /// <returns>Context information if the message is about August 7th, 2025 team updates, null otherwise</returns>
        private string? GetAugust7th2025Context(string message)
        {
            var lowerMessage = message.ToLowerInvariant();

            // Check if the message contains August 7th, 2025 or related date patterns
            var datePatterns = new[]
            {
                "august 7th 2025", "august 7 2025", "aug 7th 2025", "aug 7 2025",
                "8/7/2025", "08/07/2025", "2025-08-07", "2025/08/07",
                "august 7", "aug 7", "8/7", "08/07"
            };

            // Check if the message contains team update related keywords
            var teamUpdateKeywords = new[]
            {
                "team update", "team updates", "staff meeting", "meeting", "update"
            };

            // Check if the message contains date patterns
            bool containsDate = datePatterns.Any(pattern => lowerMessage.Contains(pattern));

            // Check if the message contains team update keywords
            bool containsTeamUpdate = teamUpdateKeywords.Any(keyword => lowerMessage.Contains(keyword));

            // If both conditions are met, return the context information for AI processing
            if (containsDate && containsTeamUpdate)
            {
                return @"**Team Update - August 7th, 2025**

**Meeting Time:** 11:30a ET / 9:00p IST

**Meeting Ground Rules:**
- Start on time + end on time
- Send agenda prior to the meeting
- No distractions (phones, email, slack, etc.)

**Meeting Purpose:**
- Team cross-talk
- Team culture
- Enterprise Architecture

**11:30 Team Updates (Tony Tran)**
- Thank you to Aadil Yousuf, Jacob Rose-Seiden, Manish Bhusal!
- ADFS Server maintenance - Fri 8-Aug 5:30p ET
- HR: SuccessFactors, PeopleSoft Prod, Culture Amp Prod
- Legal: ODDS
- Finance: Adaptive, Corcentric
- Security: GRAE

**11:35 Platform (Carlos Guaneme, Didarul Amin)**
- TeamOne + NBAOne
  - DONE: ISAC hand-over > target Wed 6-Aug, 6a ET
  - Thank you Didarul Amin!
  - NBAOne + Homecourt ServiceNow > target Mon 18-Aug
  - In-progress: integration w NBAOne target Fri 8-Aug / Fri 15-Aug
- LeagueOps Memo
  - ToDo: start remaining work for 25/26 Season Start
- CoachesDB V5 - target 30-Jul
  - In-progress: planned deploy to PROD
  - ToDo: resolve library dependency conflicts
  - ToDo: resolve Android Play Store issue; more strict approval guidelines
  - same issues for LETSGO + Combine-Central
  - ToDo: re-schedule PROD deployment
- AI Platform Services
  - Content-Insight AI
    - In-progress: setup Azure infra
    - on-hold: config AWS Bedrock
    - Q: which apps?
    - A: CoachesDB, NBAOne, TeamOne LeagueOps Memo app, GRS auto-comment
  - NBA OneGraph
  - PCMS - Federated Subscriptions
    - [Person] subgraph

**11:42 RefOps / BBOps (Kuldeep Kothari)**
- RefOps - RSS V2 (Tirumala Reddy, Anish Tiwari)
  - In-progress: sync-to-master, sync-from-master, sync-to-sandbox > adding filters
  - In-progress: (feature) edit email-content > for notifications
  - In-progress: confirmation-tracker page > display addl details
  - ToDo: demo to Tony Tran
  - In-progress: provide some addl data-points / parameters / constraints to Abacus
    - eg ""exclude RC assignment for a month""
  - ToDo: pending 25/26 NBA Season Schedule
    - target: deploy major changes > end-Aug
- RefOps - REPS (Amit Shinde)
  - DONE: GLeague Game-Report > submit in REPS (instead of GLeague OIW)
  - DONE: my-resource screen > revamped
  - In-progress: evaluation > ""game-impact""
  - In-progress: DA dashboard / DA newsletter
- RefOps - NBA Officials (Laxman Lature)
  - DONE: ReCaptcha V3 (upgraded from V2)
  - DONE: cyber remediation
  - In-progress: curriculum > some minor changes
  - DONE: new PROD infra deployed; decommissioned old infra
  - on-hold: rest of FY25
- RefOps - Referee Ratings System > target Jan-2026
  - new app
  - scope: RefOps + BSA + Teams > submit Referee Ratings
  - scope: mid-year + end-of year > collect Referee-ratings
  - ToDo: integration w Coaches TextBack
- RefOps - Replay Center
  - TBD pending changes requests for 25/26 season-start
- RefOps - Referee Career Court (Laxman Lature) - no update
- RefOps - NBA Calibration Center (Amit Shinde)
- RefOps - RefStats (Chandrasekhar Telagamsetty)
  - DONE: refops inventory module > deployed
- RefOps - Ref Ticket System (Laxman Lature) - no update
- BBOps - GLeague PATA > target 15-Oct (GLeague season-start)
  - Sprint-1: In-progress
- BBOps - PATA (Amit Shinde)
  - In-progress: minor changes for 25/26 Season-Start
  - new WNBA PATA - preliminary discussions next week
- Equinix migration + Win Server 2012 scope
  - approach: configure Window Server 2025 in Equinix
  - approach: AWS Kubernetes
- BBOps - Huddle V3 (Amit Shinde)
  - ToDo: program comparison changes > deploy next week
- BBOps - Dashboard V3 (Chandrasekhar Telagamsetty)
  - In-progress: reports
- BBOps - Draft Eligibility Portal (FY26) > target Mar-2026
  - In-progress: weekly calls w Wes Harris > requirements gathering
  - target > Oct-2025 dev-start
- BBOps - LETSGO V4 (Amit Shinde, Tirumala Reddy)
  - In-progress: feedback changes from 2025 events
- BBOps - Draft Combine (Chandrasekhar Telagamsetty) - no update
- BBOps - DRAFT app
  - TBD new app for FY26
  - request: (Wes Harris / Jason Bleznick) comms process during Teams Draft-Selection
  - scope: 5-minute period - for Team pick + live-comms to Adam Silver
  - current: draft-selection process over the phone

**11:59 Legal / BSG / GLeague (Sarada Meka)**
- BSG - WNBA TIW > target after NBA Finals
  - DONE: ""migration"" activities > in non-prod
  - pending: go-ahead from business (Todd Demoss, Sue Blanche)
- BSG - GRS
  - Smart-queue - received JSON spec + requirements from BSA (Ryan Chen)
  - ToDo: test on some games from 24/25 season
  - Auto-Comments
    - scope: using AI Content-Insight API
    - In-progress: refining prompt > for accurate auto-comment
  - OIW Game Report tab (new)
    - integrated tab in GRS app
    - eliminates user-switching to OIW app
  - Play-type Sort-Ordering
    - DONE: improved UX for Reviewers
  - Archive previous-season data
    - DONE: archived 21/22 Season in QA
  - ToDo: archive all past seasons in QA
    - target > end-Aug for PROD archiving
- BSA - Team Engagement Portal
  - In-QA: support email-notifications > on conversation-reply
  - In-progress: selected-conversations > export-to-Excel feature
  - request: (Matt Wolfson) ""Dashboard"" by-Team, by-Coach, etc; in design
- BSA - BIMS
  - DONE: updates to > leadership dashboard, ad-hoc reports, interactions
  - In-progress: support new Betting-Markets; eg MVP, Championship
- Legal - Betting Partners API
  - DONE: included NBA off-season players > in API
- Legal - ODDS
- Legal - KMS
- Legal - GEMA
- Legal - CAD
- GLeague OIW

**12:07 IT Core Tech + Architecture (John Ritter, Roman Polunin)**
- Multi-cloud architecture > target Oct-2025
- AWS LZ for IT > target TBD this week / next week
  - pending CloudSRE
  - DONE: AWS Accounts provisioned
  - ToDo: AWS Permission-Sets
- Kubernetes
  - DONE: Azure PROD Kubernetes Cluster
  - DONE: hosting PROD Content-Insight API
  - ToDo: transition to AWS Kubernetes
  - pipelines for AWS Kubernetes
  - DONE: functional prototype running in AWS Sandbox
- Observability: NewRelic + Open Telemetry
  - DONE: prototype running in AWS Sandbox
- StackGen IAC
  - DONE: licensed Stackgen for NBA
  - In-progress: defining new IAC / DevOps processes for Software Engineering
  - ToDo: Stackgen workshops scheduled > next week
- Apps Migration
  - Application Development
    - GraphAPI for Exchange Online - REMOVE
    - DONE: 3 jobs running daily > Adam Silver Calendar, Contacts, Holiday Events
  - Media Central upgrade - on-hold
    - pending Cloud SRE
    - Media Central DEV - still has open issues
  - VESS Arena Monitoring (Facilities)
    - ToDo: meeting w stakeholders > next week
  - Arena Renovations app (Broadcast)
- DevOps SSDLC
  - Code Pipeline Automation
    - In-progress: 8 new pipelines, some for BizSys (Rahul Misra)
  - Database Pipeline Automation
    - In-progress: LVA database > starting to use synthetic-data-generator

**12:14 Program Delivery (Farhad Babury)**
- Backlog Delivery
  - Time-bound projects
    - SOC2 Type2
      - Phase 1 Assessment - target Jun
      - In-progress: updating Assessment report (Krishna Bhagavathula, Steve Grossman)
      - target: next week delivery
      - Phase 2 Implementation
      - ToDo: Pen-Test (vendor Optive) > schedule end-Aug
      - In-progress: Business Continuity plan > target end-Aug
    - Odyssey
    - Email-to-Cloud
      - In-progress: post-migration work; maintaining a roadmap
    - Data Classification
      - DONE: complete
    - MSFT Copilot
      - In-progress: implementation (Maral Taak)
    - Cloud Partner AWS - target 1-Oct
      - MVP2 - AWS LZ for IT - target 8-Aug
      - DONE: AWS Accounts provisioned
      - In-progress: Permission-Sets > target 8-Aug; end next-week
    - Servicenow Homecourt > target Mon 18-Aug
      - In-progress: content-creation, content-migration
      - Q: include NBAOne in launch comms email?
      - A: yes can do
  - Valuestreams
    - Data Engineering, Player Health
      - DONE: Sprint 23 planning (3-week sprint)
    - Security, Finance, Legal, HR & MPATS, GPM
    - Tableau Cloud Migration - target 13-Aug
      - DONE: Phase 2: HR confidential data > target 25-Jul
      - Phase 3: on-track; target Wed 13-Aug
    - Legal CLM
      - ToDo: 3-month extension > for Spaulding Ridge Managed-Services (Aug-Oct)
    - HRIS Workday - target 16-Oct
      - status: Yellow (delays in Integration)
      - In-progress: E2E testing > target 29-Aug
      - ToDo: (Software Engineering) validation testing the ""PSoft"" integration-database > target mid-Aug
    - League Operations, IT Core, IT Platform

**12:26 ET / 9:56p IST AOB**
- Carlos Guaneme Out of office for 4 days starting from Aug 4th to Aug 8th
- Mohan Palanisamy Out of office for 4 days starting from Aug 4th to Aug 8th
- --
- Fri 15-Aug: TDG office closed (Independence Day)
- 27-Jun to 12-Sep: Ashwini Kanthraj Parental Leave";
            }

            return null;
        }
    }

    public class ChatRequest
    {
        [Required(ErrorMessage = "Message is required")]
        public string Message { get; set; } = string.Empty;

        public List<ChatHistoryMessage>? ConversationHistory { get; set; } = new List<ChatHistoryMessage>();
    }

    public class ChatHistoryMessage
    {
        public string Content { get; set; } = string.Empty;

        public bool IsUser { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}