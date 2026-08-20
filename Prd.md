# PermitTorch — Product Requirements Document

**Version:** 1.0
**Product:** PermitTorch
**Stage:** MVP / Validation
**Primary Market:** Fire-protection contractors
**Initial Geography:** Selected U.S. metros with reliable permit data
**Business Model:** SaaS subscription
**Core Data Source:** Existing Apify permit scraper

---

# 1. Executive Summary

PermitTorch is a lead-intelligence SaaS for fire-protection contractors.

The platform converts public permit, inspection, violation, and related municipal data into actionable sales opportunities for companies that install or service:

* Fire sprinkler systems
* Fire alarm systems
* Fire suppression systems
* Commercial kitchen suppression systems
* Fire inspections
* Fire-code compliance work

Instead of requiring contractors to manually search municipal permit portals, PermitTorch continuously collects public records, normalizes them, classifies them, scores the opportunities, and presents the most relevant leads through a searchable dashboard and email alerts.

The core value proposition is:

> **Find the permits worth chasing.**

PermitTorch is not intended to compete directly with large construction-intelligence platforms during MVP. Instead, it will provide a narrow, specialized product focused initially on fire-protection opportunities.

The MVP should prove one central hypothesis:

> Fire-protection contractors will pay recurring subscription fees for timely, filtered, scored permit intelligence that helps them identify potential projects earlier.

---

# 2. Problem

Fire-protection contractors need a continuous pipeline of new commercial projects.

Today they often acquire those opportunities through:

* Referrals
* General contractors
* Networking
* Bid platforms
* Construction intelligence platforms
* Google searches
* Cold outreach
* Existing customer relationships
* Manual permit searches

Public permit databases contain valuable sales signals, but the information is fragmented across hundreds of municipal portals.

Problems include:

* Different data structures between cities
* Poor municipal search tools
* No standardized fire-specific filtering
* No lead scoring
* No cross-city search
* No alerts
* Duplicate records
* Inconsistent permit descriptions
* Difficulty determining which permits represent meaningful opportunities
* Significant time required to manually monitor sources

PermitTorch solves this by turning raw public records into a prioritized sales feed.

---

# 3. Product Vision

PermitTorch should eventually become:

> **The sales intelligence platform for contractors that uses government permit data to identify upcoming work.**

Fire protection is the entry market.

Future trade verticals could include:

* HVAC
* Roofing
* Electrical
* Plumbing
* Solar
* Security systems
* Elevators
* Commercial cleaning
* Concrete
* General construction

However, those verticals are intentionally excluded from the initial MVP.

The immediate objective is to become exceptionally useful for one customer:

**A commercial fire-protection company looking for new jobs.**

---

# 4. MVP Goals

The MVP should accomplish five primary goals.

## Goal 1 — Prove customers will pay

Acquire an initial group of paying fire-protection contractors.

Initial validation targets:

* 3 paying customers = early validation
* 10 paying customers = meaningful validation
* $1,000+ MRR = strong signal to continue investing

---

## Goal 2 — Deliver genuinely useful leads

Users should regularly find permits they would reasonably consider contacting.

PermitTorch should help answer:

* What happened?
* Where is the project?
* What type of fire work may be required?
* How recent is it?
* How valuable does the opportunity appear?
* Why was this opportunity recommended?

---

## Goal 3 — Make permit research dramatically faster

A contractor should be able to open PermitTorch and find relevant opportunities in a few minutes instead of manually searching multiple government portals.

---

## Goal 4 — Build an SEO foundation

PermitTorch should generate indexable public content around:

* Fire-protection leads
* Fire sprinkler leads
* Fire alarm leads
* Permit activity
* Local construction activity
* Individual markets

SEO infrastructure should exist from the beginning even if the initial amount of content is small.

---

## Goal 5 — Establish a scalable data pipeline

Adding another city should primarily require adding or configuring another data source rather than rebuilding the application.

---

# 5. Non-Goals for MVP

PermitTorch will **not** initially attempt to become:

* A full CRM
* A bidding platform
* A construction project management system
* A Dodge Construction Network replacement
* A ConstructConnect replacement
* A nationwide permit API
* A contractor marketplace
* A mobile application
* An AI sales agent
* A contact enrichment platform
* A government compliance system

These may become future opportunities but would distract from validating the core business.

---

# 6. Target Users

PermitTorch will initially support four user types.

## User Type A — Contractor Owner / Business Owner

Typical company:

* Small to medium fire-protection company
* 5–100 employees
* Owner may still participate in sales
* Focused on generating more commercial work

Primary concerns:

* Finding new opportunities
* Revenue growth
* Territory expansion
* ROI from subscription
* Ease of use

---

## User Type B — Sales Representative / Business Development

Typical responsibilities:

* Finding potential projects
* Contacting developers and contractors
* Tracking opportunities
* Generating quotes
* Building pipeline

Primary concerns:

* Fresh leads
* Lead quality
* Lead priority
* Project details
* Speed

This may eventually become PermitTorch's most active user.

---

## User Type C — Sales Manager

Typical responsibilities:

* Managing multiple sales representatives
* Territory management
* Pipeline visibility
* Lead distribution
* Performance management

This user becomes more important after the MVP.

---

## User Type D — PermitTorch Administrator

Internal user.

Responsibilities:

* Monitor scraper health
* Manage sources
* Manage users
* Review data issues
* Review payments
* Inspect failed scraper runs
* Configure markets
* Correct classification problems

---

# 7. User Stories

## Contractor Owner

### Discovery

As a contractor owner, I want to see new fire-protection opportunities in my territory so that I can grow my company's pipeline.

### Value

As a contractor owner, I want to know why PermitTorch considers a project valuable so that I can determine whether the service is worth paying for.

### Territory

As a contractor owner, I want to filter opportunities to the cities or regions I service so that I do not waste time reviewing irrelevant projects.

### Subscription

As a contractor owner, I want to subscribe online without speaking with a salesperson so that I can begin using the service immediately.

### ROI

As a contractor owner, I want PermitTorch to help me identify opportunities I may otherwise have missed.

---

# 8. Sales Representative User Stories

### Lead Feed

As a salesperson, I want to see the newest opportunities first so that I can contact prospects before competitors.

### Lead Score

As a salesperson, I want opportunities ranked by quality so that I know where to focus my time.

### Lead Explanation

As a salesperson, I want to understand why a lead received its score.

Example:

**Lead Score: 92**

Signals:

* New commercial construction
* Fire sprinkler work detected
* Permit submitted within 48 hours
* Project value exceeds $1M
* Fire contractor not identified

---

### Filtering

As a salesperson, I want to filter leads by:

* Market
* Fire category
* Permit age
* Lead score
* Project type
* Permit status

so that I can quickly find the opportunities relevant to me.

---

### Saving

As a salesperson, I want to save interesting leads so that I can return to them later.

---

### External Research

As a salesperson, I want easy access to the original public record so that I can verify the information.

---

### Email Alerts

As a salesperson, I want new high-quality opportunities emailed to me automatically so that I do not have to constantly check PermitTorch.

---

# 9. Sales Manager User Stories

These should be partially supported in MVP but are not the main focus.

As a sales manager, I want multiple employees to access PermitTorch.

As a sales manager, I want the team to see the same territory.

As a sales manager, I eventually want to know which opportunities have been contacted.

Full sales pipeline functionality will not be included in MVP.

---

# 10. Administrator User Stories

As an administrator, I want to see whether each city's scraper successfully ran.

As an administrator, I want to know the last time each data source produced records.

As an administrator, I want alerts when a source appears stale.

As an administrator, I want to inspect failed scraper executions.

As an administrator, I want to disable problematic sources.

As an administrator, I want to manually modify lead classifications if necessary.

As an administrator, I want to manage users and subscriptions.

---

# 11. Core MVP Features

## Feature 1 — Authentication

Users must be able to:

* Create account
* Log in
* Log out
* Reset password
* Verify email

Recommended provider:

**Clerk**

Alternative:

* Auth0
* Supabase Auth
* Better Auth
* Custom authentication

### Recommendation

Use Clerk initially.

Authentication itself is not part of PermitTorch's competitive advantage, so development time should not be spent rebuilding authentication.

---

# 12. Permit Lead Dashboard

Primary route:

`/app/leads`

Dashboard header example:

**Houston, TX**

**42 New Opportunities**

**11 Hot Leads**

**Updated 18 minutes ago**

Main table/cards should show:

* Lead score
* Permit title
* Address
* City
* Permit category
* Project type
* Permit status
* Permit date
* Estimated value if available
* Short lead reason
* New indicator

Example:

**94 — HOT**

New Commercial Warehouse
1215 Industrial Blvd
Houston, TX

Fire Sprinkler

**Why this matters**

New commercial construction with fire sprinkler scope detected. Permit filed 2 days ago.

---

# 13. Lead Detail Page

Route:

`/app/leads/[id]`

Information should include:

### Opportunity

* Lead score
* Fire category
* Opportunity classification
* First discovered
* Last updated

### Permit

* Permit number
* Address
* City
* State
* Description
* Permit type
* Status
* Issue date
* Filing date
* Estimated project value
* Square footage

### Participants

When available:

* Owner
* Applicant
* Contractor
* General contractor

### Lead Signals

Example:

* New commercial construction
* Sprinkler work mentioned
* Permit less than 72 hours old
* Project value > $500K
* Contractor not listed

### Source

* Government source name
* Original public record link
* Last checked

---

# 14. Lead Categories

Initial categories:

* Fire Sprinkler
* Fire Alarm
* Fire Suppression
* Kitchen Suppression
* Fire Inspection
* Violation / Correction
* General Fire Protection

Potential future categories:

* Emergency lighting
* Fire extinguishers
* Smoke control
* Backflow
* Special hazard systems

---

# 15. Lead Scoring

PermitTorch should implement a deterministic scoring engine first.

Score:

**0–100**

Example signals:

| Signal                      | Example Weight |
| --------------------------- | -------------: |
| New commercial construction |            +25 |
| Explicit sprinkler scope    |            +25 |
| Explicit fire alarm scope   |            +20 |
| Failed inspection           |            +20 |
| Permit <72 hours old        |            +15 |
| Project value >$500K        |            +10 |
| Large square footage        |            +10 |
| Contractor not listed       |            +10 |
| Old permit                  |            -20 |
| Closed permit               |            -30 |

Exact weights should remain configurable.

### Important

Lead scoring should initially be rule-based.

Do **not** rely on an LLM to calculate the primary score.

LLMs may eventually assist classification, but deterministic scoring will be:

* Cheaper
* Faster
* Easier to debug
* Easier to explain
* More consistent

---

# 16. Lead Score Explanation

Every score should be explainable.

Users should see:

### Why this is a 91

+25 New commercial project
+20 Sprinkler scope detected
+15 Filed within 48 hours
+15 Project value > $1M
+10 No fire contractor listed
+6 Large commercial property

This is an important differentiator.

---

# 17. Search and Filtering

Users should be able to search:

* Address
* Permit number
* Description
* Company
* City

Filters:

### Location

* Metro
* City

### Category

* Sprinkler
* Alarm
* Suppression
* Inspection
* Violation

### Lead Score

* 90–100
* 80–89
* 70–79
* All

### Age

* Today
* Last 3 days
* Last 7 days
* Last 30 days

### Status

* New
* Active
* Inspection
* Failed
* Closed

---

# 18. Saved Leads

Users can click:

**Save Lead**

Saved leads appear under:

`/app/saved`

MVP does not need sophisticated CRM functionality.

Optional simple status:

* Saved
* Contacted

Avoid building a full sales pipeline initially.

---

# 19. Email Digest

Email is considered an MVP feature.

Users should be able to choose:

**Daily Digest**

or

**Weekly Digest**

Example:

# 14 New Houston Fire Opportunities

### 🔥 94 — Commercial Sprinkler

$2.8M warehouse project
Houston, TX
Filed yesterday

### 🔥 89 — Fire Alarm

Apartment renovation
Houston, TX
Filed 2 days ago

CTA:

**View All Opportunities**

---

# 20. Email Infrastructure

Recommended:

**Resend**

Reasons:

* Developer-friendly
* Good Next.js integration
* Simple transactional email API
* Appropriate for MVP

Alternatives:

* Postmark
* SendGrid
* AWS SES

SES may eventually become cheaper at scale, but Resend provides faster development.

---

# 21. Public Marketing Website

Public pages should be SEO-friendly and server-rendered.

Required pages:

`/`

`/pricing`

`/how-it-works`

`/fire-protection-leads`

`/fire-sprinkler-leads`

`/fire-alarm-leads`

`/locations`

`/locations/[state]/[city]`

`/blog`

`/login`

`/signup`

---

# 22. Homepage

Hero:

# Find the permits worth chasing.

PermitTorch monitors public permit and inspection records and identifies fire-protection opportunities before they disappear into another spreadsheet.

CTA:

**Find Leads in Your Market**

Secondary CTA:

**See Sample Leads**

Homepage should explain:

1. PermitTorch monitors public records.
2. PermitTorch identifies fire-related opportunities.
3. PermitTorch scores them.
4. Contractors receive actionable leads.

---

# 23. SEO City Pages

Example:

`/fire-protection-leads/houston-tx`

or:

`/locations/texas/houston`

Potential page content:

# Fire Protection Leads in Houston, Texas

PermitTorch discovered:

**137 fire-related opportunities during the last 30 days**

* 52 sprinkler
* 37 alarm
* 18 suppression
* 12 inspection failures
* 18 other

Page should include:

* Market description
* Recent aggregate permit activity
* Categories
* Last update time
* Example anonymized leads
* CTA
* FAQ

---

# 24. Important SEO Rule

Avoid creating thousands of low-value programmatic pages.

A city page should exist only when PermitTorch has:

* Active data coverage
* Real records
* Meaningful statistics
* Useful market information

Each page should provide actual data rather than simply swapping city names into identical content.

---

# 25. Blog

Initial blog topics:

* How Fire Sprinkler Contractors Find Leads
* How to Find Commercial Fire Protection Projects
* Using Building Permits for Lead Generation
* How Fire Alarm Companies Find New Projects
* What Fire Contractors Can Learn From Permit Data
* How to Find Construction Projects Before They Are Bid
* Fire Sprinkler Leads in Houston
* Fire Protection Sales Strategies

Educational SEO should support transactional landing pages.

---

# 26. Pricing

Initial proposed pricing:

## Starter

**$49/month**

* 1 market
* Weekly digest
* Basic filtering
* Limited historical records

---

## Pro

**$129/month**

* 1 market
* Daily updates
* Full lead scoring
* Saved opportunities
* Daily email digest
* CSV export
* 30–90 days history

Recommended / highlighted plan.

---

## Territory

**$249/month**

* Up to 5 markets
* Multiple users
* Advanced filters
* Daily alerts
* Historical records
* Priority support

---

## Future Enterprise

Custom pricing.

Potential:

* Statewide coverage
* Nationwide coverage
* API
* Large teams
* CRM integration

---

# 27. Payments

Recommended:

**Stripe Billing**

Required functionality:

* Subscription checkout
* Free trial
* Upgrade
* Downgrade
* Cancellation
* Customer billing portal
* Failed payment handling
* Stripe webhooks

---

# 28. Recommended Technical Architecture

## Frontend

**Next.js + TypeScript**

Recommended version:

Current stable Next.js version at development time.

Reasons:

* React ecosystem
* Server-side rendering
* Excellent SEO support
* Dynamic metadata
* Static pages
* Server components
* API capabilities
* Strong hosting ecosystem

---

# 29. UI

Recommended:

**Tailwind CSS**

*

**shadcn/ui**

Advantages:

* Rapid development
* Highly customizable
* Professional SaaS appearance
* No large proprietary component dependency

---

# 30. Backend

Recommended architecture:

**Next.js**

↓

**ASP.NET Core API**

↓

**PostgreSQL**

↓

**Apify**

This is slightly more infrastructure than a pure Next.js MVP but provides an excellent long-term foundation.

Because PermitTorch may eventually become a data-heavy platform, keeping core domain logic inside an API is reasonable.

---

# 31. ASP.NET Core Responsibilities

The C# API should handle:

* Lead retrieval
* Filtering
* Lead scoring
* Saved leads
* User market access
* Subscription authorization
* Permit ingestion
* Data normalization
* Deduplication
* Admin operations

---

# 32. Alternative Faster Architecture

An even faster MVP would be:

**Next.js**

↓

**PostgreSQL**

↓

**Apify**

with Next.js server actions/API routes handling backend functionality.

### Advantage

Less infrastructure.

### Disadvantage

Business logic may eventually become tightly coupled to the web application.

### Recommendation

Given the expected complexity of PermitTorch's data pipeline, a standalone ASP.NET Core API is reasonable.

However:

Do **not** create microservices.

Start with a modular monolith.

---

# 33. Database

Recommended:

**PostgreSQL**

Core tables:

```text
users

organizations

subscriptions

markets

sources

permits

permit_participants

inspections

violations

fire_opportunities

lead_signals

saved_leads

scraper_runs

email_preferences
```

---

# 34. Permit Data Model

Example:

```text
Permit

id
external_id
source_id

permit_number
permit_type
description
status

address
city
state
zip

latitude
longitude

filed_date
issued_date

estimated_value
square_footage

owner_name
contractor_name

source_url

first_seen_at
last_seen_at

created_at
updated_at
```

---

# 35. Fire Opportunity

```text
FireOpportunity

id
permit_id

category

lead_score
confidence

reason

first_detected_at
last_updated_at
```

---

# 36. Lead Signal

```text
LeadSignal

id
fire_opportunity_id

signal_type
description
weight
```

Example:

```text
NEW_COMMERCIAL_BUILD
+25

FIRE_SPRINKLER_SCOPE
+25

PERMIT_RECENT
+15
```

---

# 37. Source Health

This is extremely important.

Each municipal source should maintain:

```text
Source

id
name
city
state

portal_type
source_url

active

last_successful_run
last_record_seen

records_last_run

health_status
```

Possible health status:

```text
HEALTHY

WARNING

STALE

FAILED

DISABLED
```

---

# 38. Scraper Architecture

Do not trigger Apify searches directly from user requests.

Instead:

```text
Municipal Portal
      ↓
Apify Actor
      ↓
Raw Dataset
      ↓
PermitTorch Ingestion
      ↓
Normalize
      ↓
Deduplicate
      ↓
Classify
      ↓
Score
      ↓
PostgreSQL
      ↓
API
      ↓
Website
```

This prevents:

* Slow searches
* Large Apify costs
* Duplicate scraper executions
* Unpredictable user experience

---

# 39. Apify Integration

Apify should remain the data collection layer.

PermitTorch should call Apify using:

* Actor API
* Scheduled Actor execution

Possible model:

Apify schedule runs each source periodically.

PermitTorch ingestion job checks:

* Actor run status
* Dataset
* New records

Alternative:

Apify webhook triggers PermitTorch ingestion after successful run.

---

# 40. Potential Pitfall — Apify Vendor Dependency

PermitTorch should not allow raw Apify implementation details to leak deeply throughout the application.

Create an abstraction:

```text
PermitSourceProvider
```

Example:

```text
ApifyPermitProvider
```

Future:

```text
DirectArcGISProvider
SocrataProvider
CKANProvider
AccelaProvider
```

This prevents PermitTorch from being permanently dependent on Apify.

---

# 41. Hosting

Recommended MVP deployment:

### Frontend

**Vercel**

### ASP.NET API

**Railway**

### PostgreSQL

**Railway PostgreSQL**

or managed Postgres provider.

This combination prioritizes:

* Speed
* Simplicity
* Low DevOps overhead

AWS should not be required during initial validation.

---

# 42. Why Not AWS Initially?

AWS provides excellent scale but also introduces:

* IAM configuration
* Networking
* ECS
* ALB
* Security groups
* Secrets
* Logging
* RDS configuration
* More infrastructure maintenance

There is little reason to introduce this burden while validating whether contractors will pay.

PermitTorch can migrate infrastructure later if necessary.

---

# 43. Background Jobs

PermitTorch will require jobs for:

* Data ingestion
* Source health checks
* Email digest generation
* Expiring stale records
* Aggregate SEO statistics

Recommended options:

### Initial

Background job inside ASP.NET Core.

### Later

Hangfire.

Potential alternatives:

* Temporal
* BullMQ
* Trigger.dev

Do not introduce distributed job infrastructure until necessary.

---

# 44. Search

Initially use:

**PostgreSQL search**

using:

* indexes
* `ILIKE`
* full-text search when needed

Do not introduce Elasticsearch immediately.

Do not introduce Azure AI Search.

Do not introduce Pinecone.

PermitTorch does not need vector search for MVP.

---

# 45. Caching

Potential:

**Redis**

But not required initially.

Start without Redis unless performance proves otherwise.

PostgreSQL + sensible indexes should handle the MVP easily.

---

# 46. AI / LLM Usage

AI should be limited during MVP.

Potential useful application:

Classifying ambiguous permit descriptions.

Example:

```text
"INT ALT / RECONFIG LIFE SAFETY SYSTEM"
```

could be classified into:

```text
FIRE_ALARM
```

However, classification should ideally use:

1. Deterministic keyword rules
2. Regex / pattern rules
3. LLM fallback when confidence is low

This reduces:

* Cost
* Latency
* Hallucination risk

---

# 47. Do Not Use RAG for MVP

PermitTorch currently has no significant RAG requirement.

RAG could eventually support:

> "Show me Houston restaurants that failed fire inspections this month."

But normal SQL filtering can solve most initial queries faster and more reliably.

---

# 48. Observability

Minimum:

**Sentry**

for:

* Frontend exceptions
* API exceptions

Application logging should record:

* Apify run ID
* Source
* Import count
* Duplicate count
* Classification count
* Failures
* Processing duration

---

# 49. Analytics

Recommended:

**PostHog**

Track:

* Signup
* Pricing page view
* Lead opened
* Lead saved
* Search performed
* Filter changed
* Digest enabled
* Checkout started
* Subscription created

These events will help determine whether users actually receive value.

---

# 50. SEO Technical Requirements

Required:

* Server-rendered pages
* Unique title
* Unique meta description
* Canonical URL
* OpenGraph metadata
* Structured data where appropriate
* XML sitemap
* robots.txt
* Fast page load
* Responsive design
* Proper internal linking

---

# 51. Responsive Design

PermitTorch should be fully usable on:

* Desktop
* Tablet
* Mobile

The primary experience should remain desktop-oriented because sales professionals may use PermitTorch during work.

No native iOS or Android application is required.

---

# 52. MVP Navigation

Public:

```text
PermitTorch

Leads
Markets
How It Works
Pricing
Resources

Login
Start Free
```

Application:

```text
Overview

Leads

Saved

Alerts

Account
```

Admin:

```text
Sources

Runs

Users

Subscriptions
```

---

# 53. MVP Admin Dashboard

At minimum show:

### Source Health

Houston

**Healthy**

Last successful run:

6 minutes ago

Records:

247

---

Dallas

**Warning**

Last successful run:

19 hours ago

Records:

0

---

Admin should be able to investigate the source.

---

# 54. Notifications

MVP:

Email.

Future:

* SMS
* Slack
* Teams
* Push notifications
* CRM notifications

Avoid building these initially.

---

# 55. CSV Export

Pro users should be able to export filtered opportunities.

Columns:

* Score
* Address
* City
* Permit type
* Fire category
* Description
* Permit date
* Project value
* Owner
* Contractor
* Source URL

---

# 56. Market Access

Subscriptions should control access to markets.

Example:

Starter:

```text
Houston
```

Territory:

```text
Houston
Dallas
Austin
San Antonio
Fort Worth
```

Market-level entitlement should exist in the data model from the beginning.

---

# 57. Organization Model

Even if MVP users begin individually, use an organization model.

```text
Organization

Acme Fire Protection

Users

Andy
Mike
Sarah

Markets

Houston
Dallas
```

This prevents painful restructuring later when companies want multiple seats.

---

# 58. Security

Requirements:

* HTTPS
* Secure authentication provider
* Server-side authorization
* Stripe webhook signature verification
* Rate limiting
* Input validation
* Parameterized SQL / ORM
* Secrets stored outside source control
* Role-based admin authorization

Never rely purely on hidden frontend elements to secure premium markets.

---

# 59. Data Legality / Compliance

PermitTorch should store:

* Source URL
* Government jurisdiction
* Retrieval timestamp

Terms of service and privacy policy should clearly explain:

* Data is collected from publicly available records
* PermitTorch does not guarantee accuracy
* Users should independently verify opportunities
* Records may be delayed or corrected by jurisdictions

Source-specific terms should be reviewed before commercializing each source.

---

# 60. Data Quality Challenge

The largest technical risk is not frontend development.

It is:

**data reliability.**

Municipal sources may:

* Change APIs
* Remove fields
* Rename fields
* Freeze datasets
* Break pagination
* Return duplicates
* Change ArcGIS layers
* Introduce CAPTCHAs
* Change Accela behavior
* Temporarily stop publication

Therefore source monitoring is an MVP requirement, not a future enhancement.

---

# 61. Data Freshness Indicator

Each market should show:

**Updated 18 minutes ago**

If data becomes stale:

**Data last updated 2 days ago**

Admin alert thresholds should be configurable.

Never silently present stale data as current.

---

# 62. Deduplication

Duplicate permits would quickly destroy customer trust.

Use:

```text
source_id
+
external_permit_id
```

as the preferred unique identifier.

Fallback fingerprint may combine:

```text
address
permit_type
filed_date
description
```

Store source records historically rather than blindly overwriting data.

---

# 63. Historical Changes

Eventually PermitTorch should know:

```text
Permit submitted

↓

Permit issued

↓

Inspection scheduled

↓

Inspection failed

↓

Reinspection

↓

Approved
```

For MVP, store the latest state plus timestamps where possible.

Avoid implementing a complete event sourcing system.

---

# 64. Features Explicitly Excluded From MVP

## CRM

No complex:

* pipelines
* activities
* tasks
* sales forecasting
* opportunity management

Only:

* Save
* Optional Contacted status

---

## Contact Enrichment

Do not initially purchase/enrich:

* Emails
* Phone numbers
* LinkedIn profiles
* executive data

Potential Phase 2/3 feature.

---

## AI Assistant

No chatbot.

---

## Native Mobile App

No Swift or Android application.

---

## Nationwide Coverage

Start with limited reliable markets.

---

## API Product

Do not build a customer-facing API.

---

## Territory Exclusivity

Interesting future pricing strategy but not MVP.

---

## Automated Outreach

PermitTorch should not initially email prospects on behalf of contractors.

---

## CRM Integrations

No:

* Salesforce
* HubSpot
* Pipedrive

during MVP.

CSV export is enough.

---

# 65. MVP Markets

Do not select markets purely by population.

Score potential markets based on:

### Data quality

Is the source reliable?

### Fire specificity

Can PermitTorch identify meaningful fire-related information?

### Frequency

How frequently is the dataset updated?

### Lead volume

Are enough opportunities generated?

### Contractor density

Are there enough potential customers?

### Operational complexity

How fragile is the source?

---

# 66. Market Health Score

PermitTorch could maintain an internal score:

```text
Houston

Data Reliability: 95
Fire Classification: 92
Freshness: 97
Lead Volume: 90

Overall: 94
```

Markets below a threshold should not be sold.

---

# 67. MVP Marketing Strategy

The first marketing channel should be:

**direct outbound.**

SEO should begin immediately but will take time.

Initial acquisition should use:

1. Google Maps
2. Contractor websites
3. LinkedIn
4. Fire-protection associations/directories
5. Local contractor lists

---

# 68. Initial Outreach Strategy

Target a small number of highly relevant companies.

Instead of:

> We built a permit intelligence platform.

Use:

> We found 12 new commercial projects in Houston this week that appear to need fire sprinkler or alarm work.

Offer sample leads.

PermitTorch's own data becomes its best sales material.

---

# 69. Free Lead Magnet

Create:

# This Week's Hottest Fire Protection Opportunities in Houston

User provides:

* Name
* Work email
* Company
* Market

PermitTorch provides:

* 5–10 sample leads

Then upsell:

**Get new opportunities every morning.**

---

# 70. Trial Strategy

Potential structure:

### Free Account

Users see:

* Lead counts
* Three complete leads
* Other leads partially locked

CTA:

**Unlock Houston**

Then:

7-day Pro trial.

This may demonstrate value better than giving unlimited anonymous access.

---

# 71. SEO Marketing Strategy

Three content categories.

## Commercial

```text
fire protection leads
fire sprinkler leads
commercial fire leads
fire alarm leads
```

## Local

```text
fire sprinkler leads Houston
fire protection leads Dallas
commercial permit leads Seattle
```

## Educational

```text
how fire sprinkler companies find leads
how to find commercial construction projects
building permit lead generation
```

---

# 72. Data-Driven SEO

One of PermitTorch's strongest potential advantages is proprietary aggregated data.

Example:

# Houston Fire Permit Activity — August 2026

PermitTorch recorded:

* 311 fire-related permits
* 127 sprinkler opportunities
* 89 alarm opportunities
* 31 suppression permits

This content is much harder for competitors to replicate than generic SEO articles.

---

# 73. Product Metrics

Track:

## Product

* Leads generated/day
* Leads generated/market
* Hot leads/day
* Duplicate percentage
* Classification accuracy
* Source uptime
* Average freshness

## User

* Leads viewed/week
* Leads saved/week
* Search frequency
* Digest open rate
* Return rate

## Conversion

* Visitor → signup
* Signup → trial
* Trial → paid
* Pricing → checkout
* Sample lead → signup

## Business

* MRR
* ARPU
* CAC
* Churn
* LTV

---

# 74. North Star Metric

Initial:

> **Qualified leads viewed by paying users per week**

Eventually:

> **PermitTorch leads that result in customer outreach or quotes**

The second metric is far more meaningful but harder to measure.

---

# 75. Initial Success Criteria

Continue aggressively investing if PermitTorch reaches:

### Validation

3–5 paying fire-protection companies.

### Early traction

10+ customers.

### Strong signal

$1,000+ MRR with customers regularly viewing leads.

### Excellent signal

Customers report that PermitTorch-generated opportunities resulted in:

* Calls
* Meetings
* Quotes
* Contracts

---

# 76. Potential Technical Pitfalls

## Pitfall 1 — Overengineering

Avoid:

* Kubernetes
* Microservices
* Kafka
* Elasticsearch
* Vector databases
* complicated event systems

The customer does not care.

---

## Pitfall 2 — Scraper reliability

This is the biggest operational concern.

Build monitoring before adding dozens of sources.

---

## Pitfall 3 — Poor normalization

Municipal fields will differ.

Create a canonical PermitTorch model.

Never let city-specific structures leak into the frontend.

---

## Pitfall 4 — Bad scoring

A sophisticated-looking score that produces poor leads will destroy trust.

Make scores explainable and continuously tune them based on user behavior.

---

## Pitfall 5 — Low-value SEO pages

Do not create thousands of city pages without actual data.

---

## Pitfall 6 — Data freshness

Contractors care heavily about timing.

A mediocre lead discovered today can be more valuable than an amazing project discovered three months late.

---

## Pitfall 7 — Building before selling

The biggest business risk is not technical.

It is building a polished platform before proving companies will pay.

---

# 77. Recommended Repository Structure

Potential monorepo:

```text
permittorch/

apps/

  web/
    Next.js

  api/
    ASP.NET Core

packages/

  ui/

  types/

  config/

infra/

docs/
```

Or two repositories:

```text
permittorch-web

permittorch-api
```

### Recommendation

Use a monorepo initially if comfortable with it.

---

# 78. Backend Structure

```text
PermitTorch.Api

Features/

  Leads/
  Markets/
  SavedLeads/
  Sources/
  Subscriptions/
  Organizations/
  Admin/

Domain/

Infrastructure/

Jobs/

Data/
```

Prefer vertical slices/features over giant generic folders such as:

```text
Controllers
Services
Repositories
Models
```

when the application becomes larger.

---

# 79. API Examples

```text
GET /api/leads

GET /api/leads/{id}

GET /api/markets

GET /api/saved-leads

POST /api/saved-leads

DELETE /api/saved-leads/{id}

GET /api/account/markets

PUT /api/email-preferences
```

Admin:

```text
GET /api/admin/sources

GET /api/admin/scraper-runs

POST /api/admin/sources/{id}/disable
```

---

# 80. Initial Development Phases

## Phase 0 — Existing Scraper

Complete and stabilize:

* Fire permit extraction
* Normalization
* Deduplication
* Basic scoring
* Apify deployment

---

## Phase 1 — Marketing Validation

Build:

* Homepage
* Pricing
* Houston page
* Two additional market pages
* Sample leads
* Email capture
* Weekly lead digest

Goal:

**Acquire first paying customers.**

---

## Phase 2 — SaaS MVP

Build:

* Authentication
* Organization
* Stripe
* Lead dashboard
* Filters
* Lead details
* Saved leads
* Email digest
* Market entitlements
* Admin source monitoring

Goal:

**10 paying users.**

---

## Phase 3 — Growth

Build:

* More markets
* Better classification
* Data-driven SEO reports
* More sophisticated alerts
* Team accounts
* Contact enrichment experiment
* CRM status tracking

---

## Phase 4 — Expansion

Potential:

* Additional contractor verticals
* API
* CRM integrations
* AI search
* Territory exclusivity
* Nationwide plans

---

# 81. Suggested MVP Build Priority

## P0 — Required

* Permit ingestion
* Canonical data model
* Deduplication
* Fire classification
* Lead scoring
* Source monitoring
* Public website
* SEO infrastructure
* Authentication
* Lead feed
* Lead details
* Filters
* Market access
* Stripe
* Email digest
* Basic admin dashboard

---

## P1 — Strongly Desired

* Saved leads
* CSV export
* Free sample leads
* Lead score explanation
* Data freshness UI
* Usage analytics

---

## P2 — Later

* Contact enrichment
* CRM
* AI assistant
* Salesforce
* HubSpot
* SMS
* Slack alerts
* Territory exclusivity
* Native mobile app
* Other trades

---

# 82. Recommended Stack Summary

| Layer               | Technology                       |
| ------------------- | -------------------------------- |
| Marketing + Web App | Next.js / TypeScript             |
| UI                  | Tailwind + shadcn/ui             |
| API                 | ASP.NET Core                     |
| Database            | PostgreSQL                       |
| Data Collection     | Apify                            |
| Authentication      | Clerk                            |
| Billing             | Stripe                           |
| Email               | Resend                           |
| Analytics           | PostHog                          |
| Error Monitoring    | Sentry                           |
| Frontend Hosting    | Vercel                           |
| API Hosting         | Railway                          |
| Database Hosting    | Railway / managed PostgreSQL     |
| SEO                 | Next.js SSR/SSG                  |
| Search              | PostgreSQL                       |
| Background Jobs     | ASP.NET initially                |
| AI                  | Optional classification fallback |

---

# 83. Final Recommendation

PermitTorch should resist becoming a giant platform during its first release.

The technical product required to validate the business is relatively simple:

```text
Reliable permit data

+

Good fire classification

+

Useful lead scoring

+

Fast dashboard

+

Daily email

+

Subscription billing
```

Everything else is secondary.

The strongest competitive advantage is unlikely to be the frontend or even the scraper itself.

It will come from the combination of:

**Data coverage**

*

**Data freshness**

*

**Data normalization**

*

**Fire-specific classification**

*

**Lead scoring**

*

**Historical knowledge**

*

**Understanding which signals actually result in contractor sales**

PermitTorch should therefore prioritize data quality and customer feedback over engineering complexity.

The most important milestone is not launching the website.

It is reaching the point where a fire-protection contractor says:

> **“PermitTorch showed me a job I wouldn't have found otherwise.”**

Once that happens repeatedly, the product has something worth scaling.
