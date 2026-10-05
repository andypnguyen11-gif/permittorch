# Legal research: first pass, for the lawyer to check

**Status: first pass. Statutes and regulations, and case law for questions 1, 2, 8, 9, 17 and 21. Not checked by a lawyer.** Written 2026-09-29. Case law added the same day.

This file goes with `docs/legal-review.md`. It was not written by a lawyer and is not legal advice. It records what the statutes, regulations and court opinions say on the brief's questions, so that the lawyer checks conclusions instead of starting from nothing. Nothing here says the product is lawful.

## How to read it

Every finding carries one of four marks.

- **Read in the text.** The section was read on 2026-09-29, on the official state site or on Cornell LII for federal law. Texas and Oregon were read on `public.law`, which is not official.
- **Read in the opinion.** The court's opinion was opened on CourtListener on 2026-09-29. Where only part was read, the entry says which part.
- **Commentary only.** Taken from a law firm or trade article. Not checked against the statute.
- **Not researched.** Nobody has looked yet. It does not mean no rule exists.

Quotations were taken through an automated page reader. Check each against the source before relying on its exact words.

How the case law was done:

- Only CourtListener was used. It does not hold every opinion. Where a search found nothing, the entry says "nothing found" and lists the searches. That means nothing was found there, not that no case exists.
- CourtListener does not mark a case as overruled or questioned. The checks made are in "Whether the cases still stand" near the end. They are weaker than a citator. The lawyer should run each case through KeyCite or Shepard's.
- District court orders bind no other court. They are included because few appeal courts have ruled on scraping of public data.
- Each entry records what the court said. None says whether the product complies.

## Where the markets are

32 markets in 24 states and the District of Columbia:
AZ, CA, CO, DC, FL, GA, IL, KY, LA, MA, MD, MI, MN, MO, NC, NE, NY, OH, OK, OR, PA, TN, TX, VA, WA.

A person named on a permit may live in any state. State privacy laws follow the person's residence, not the city that issued the permit.

## The finding that decides most of group 2

State privacy and data broker laws mostly leave out information that is lawfully available from government records. Every contact detail in the product comes from a government permit record, so this exclusion is the main question for the lawyer in group 2.

| Law | What the text says | Mark |
|---|---|---|
| California CCPA, Civ. Code § 1798.140(v)(2) | "Publicly available" covers information "lawfully made available from federal, state, or local government records". It is outside "personal information". | Read in the text |
| California data broker law, Civ. Code § 1798.99.80 | "The definitions in Section 1798.140 shall apply unless otherwise specified in this title." A data broker is "a business that knowingly collects and sells to third parties the personal information of a consumer with whom the business does not have a direct relationship." | Read in the text |
| Texas data broker law, Bus. & Com. Code § 509.001, § 509.002(b) | "Personal data" excludes publicly available information, which includes information "lawfully made available through government records". The chapter lists publicly available information among the data it does not apply to. | Read in the text (unofficial site) |
| Oregon data broker law, ORS 646A.593 | A data broker does not include a business "providing information that is lawfully available from federal, state or local government records". | Read in the text (unofficial site) |
| Vermont data broker law, 9 V.S.A. § 2430 | Excludes "publicly available information to the extent that it is related to a consumer's business or profession". This is narrower: it does not cover a homeowner. | Read in the text |
| Virginia VCDPA, § 59.1-575 | "Personal data" does not include publicly available information, defined to include information "lawfully made available through federal, state, or local government records". | Read in the text |
| Oregon OCPA, ORS 646A.570 | Personal data excludes information "lawfully available through federal, state or local government records". | Read in the text (unofficial site) |

**For the lawyer:**

1. Does the exclusion still hold once a record is copied, scored, combined with other permits and sold? The statutes say "lawfully made available". They do not say what happens after.
2. Vermont has no markets, but its exclusion does not reach a homeowner who lives there and is named on a permit elsewhere. Does that matter at our size?
3. If a portal's terms forbid automated collection, is the information still "lawfully" made available to us?

**Case law on question 17 (does the exclusion survive copying and resale): nothing found.**

No opinion was found that decides whether information from government records stays "publicly available" under a state privacy or data broker law after it is copied, combined and resold. Searches run on CourtListener, opinions only:

| Search | Result |
|---|---|
| Meaning search: "Does personal information obtained from government records remain publicly available and exempt from privacy law when a data broker compiles and resells it?", filed since 2018 | First 20 results looked at by name. They were public records request cases and unrelated cases. None, by name, turns on a state privacy law. |
| "publicly available" and "government records" with "1798.140" or "Consumer Privacy Act" or "Consumer Data Protection Act" or "data broker", filed since 2019 | 1 result: State of Texas v. Arity 875, LLC (Tex. App. 15th Dist. 2025). Searched inside, not read in full. The phrase appears only where the Texas definitions are reprinted. No ruling on the question was found in it. |
| "Daniel's Law" with "data broker" or "home address" and "First Amendment" | 0 results |
| "Atlas Data Privacy" and "Daniel's Law" | 0 results |

Not searched: First Amendment cases on publishing facts taken from public records, and federal Driver's Privacy Protection Act cases. Both may bear on this question and on question 18.

## Group 1: collecting and reselling public records

**Question 1 (automated collection of public data and the Computer Fraud and Abuse Act).**

The Computer Fraud and Abuse Act (CFAA), 18 U.S.C. § 1030, forbids getting information from a computer "without authorization" or by one who "exceeds authorized access".

| Case | What the court held | Our facts, and where it binds | Mark |
|---|---|---|---|
| Van Buren v. United States, U.S. Supreme Court, 2021, 593 U.S. 374. [Link](https://www.courtlistener.com/opinion/4888668/van-buren-v-united-states/) | A person "exceeds authorized access" only by getting information from parts of a computer, "such as files, folders, or databases", that are off limits to him. Using access he does have for a forbidden purpose is not enough. Footnote 8 leaves open whether the limits that count are only technical ones or also those "contained in contracts or policies". | Not close. A police officer used his own login to search a police database for pay. Not a scraping case. Binds every court in the United States. | Read in the opinion. Syllabus and the Court's opinion read in full. Dissent not read. |
| hiQ Labs, Inc. v. LinkedIn Corp., 9th Cir., 2022, 31 F.4th 1180. [Link](https://www.courtlistener.com/opinion/6460342/hiq-labs-inc-v-linkedin-corporation/) | Upheld an early order that stopped LinkedIn from blocking hiQ. "It is likely that when a computer network generally permits public access to its data, a user's accessing that publicly available data will not constitute access without authorization under the CFAA." The court also said that those who are scraped "are not without resort": trespass to chattels, copyright, misappropriation, unjust enrichment, breach of contract and breach of privacy claims may remain. This was a ruling on "serious questions" at an early stage, not a final ruling. | Partly close. hiQ used bots to collect data that anyone could see without logging in, and sold products built on it to businesses. Differences: the site owner was a private company that had sent a letter telling hiQ to stop; the data was posted by the people it described. Binds the Ninth Circuit. Of our states: AZ, CA, OR, WA. | Read in the opinion. Read in full. |
| Fidlar Technologies v. LPS Real Estate Data Solutions, Inc., 7th Cir., 2016, 810 F.3d 1075. [Link](https://www.courtlistener.com/opinion/8442520/fidlar-technologies-v-lps-real-estate-data-solutions-inc/) | A data company wrote its own program to download county land records in bulk through a vendor's system. It went around the vendor's viewing software and its per-page print fees. The court found no intent to defraud and no "damage" under the CFAA, and no breach of the Illinois computer crime law. The county agreements did not forbid a harvesting program. "Fidlar attempts to convert its failure to prohibit LPS's action by contract into an allegation of criminal conduct." | The closest found on the kind of data: government records, collected in bulk, by a company that sells data. Differences: LPS had a paid subscription and a login with each county; we read open portals without logging in. The software vendor sued, not a government. Binds the Seventh Circuit. Of our states: IL. | Read in the opinion. Read in full. |
| Meta Platforms, Inc. v. Bright Data Ltd., N.D. Cal., order of 2024-01-23, No. 3:23-cv-00077-EMC, docket entry 181. No reporter citation found. [Link](https://www.courtlistener.com/docket/66706470/181/meta-platforms-inc-v-bright-data-ltd/) | Judgment for the scraper on breach of contract. Meta showed no scraping behind a login. The court separated getting past anti-bot measures from getting past a password: using a program to get past a CAPTCHA "is different from accessing a password-protected site". The contract findings are under question 2. | Partly close. Bright Data collected public data without logging in and sold it as data sets. Differences: a private platform; the ruling turned on the wording of Meta's terms under California law. Binds no other court. | Read in the opinion. Read in full. |
| X Corp. v. Bright Data Ltd., N.D. Cal., orders of 2024-05-09 and 2024-11-26, No. 3:23-cv-03698-WHA, docket entries 83 and 156. No reporter citation found. [Link to 83](https://www.courtlistener.com/docket/67637345/83/x-corp-v-bright-data-ltd/), [link to 156](https://www.courtlistener.com/docket/67637345/156/x-corp-v-bright-data-ltd/) | May 2024: complaint dismissed. Claims about access failed because X pleaded no harm to its servers: "harmless interference cannot constitute trespass to chattels". State law claims about copying and selling the data were held to be overridden by the federal Copyright Act. November 2024: after X pleaded server failures and the cost of extra capacity, claims for trespass to chattels and for breach of contract based on access were allowed to go forward, and CFAA and state computer crime claims were allowed to be added without a ruling on them. The copying and selling claims were refused again. "This is an evolving area of state law and as pleaded here is factually sensitive." | Partly close, for the same reasons as Meta. The content there was users' posts. Whether the Copyright Act reasoning carries over to permit records was not addressed. Binds no other court. The docket shows a joint request to dismiss filed 2025-06-27 and the case closed 2025-07-01. No final ruling on the merits was found. | Read in the opinion. Both orders read in full. |

**Nothing found:** no opinion was found in which a city, county or state sued or prosecuted anyone for automated collection from an open-data portal or a public permit website. Searches run on CourtListener, opinions only:

| Search | Result |
|---|---|
| Meaning search: "Does automated scraping of publicly available website data without a login violate the Computer Fraud and Abuse Act after Van Buren?", filed since 2021-06-03 | First 20 results looked at by name. None is a suit by a government over scraping of its own site. |
| scraping with "public records" or "government website" or "county" or "court records", and "Computer Fraud and Abuse Act" or "terms of use" or "trespass to chattels", filed since 2015 | 12 results. Fidlar read. None of the others is a suit by a government over scraping of its own site, judged by case name. |

No appeal court opinion on scraping was read from the First, Third, Fourth, Fifth, Sixth, Eighth, Eleventh or D.C. Circuits. Most of our market states are in those circuits.

**Question 2 (portal terms we never clicked to accept; breach of contract and trespass).**

| Case | What the court held | Our facts, and where it binds | Mark |
|---|---|---|---|
| Register.com, Inc. v. Verio, Inc., 2d Cir., 2004, 356 F.3d 393. [Link](https://www.courtlistener.com/opinion/784889/registercom-inc-v-verio-inc/) | Verio's robot queried a public database of domain name owners every day and used the contact details for sales calls, email and mail. The terms came back with each answer. Verio never clicked anything, and admitted it knew the terms. The court held Verio was likely bound. "It is standard contract doctrine that when a benefit is offered subject to stated conditions, and the offeree makes a decision to take the benefit with knowledge of the terms of the offer, the taking constitutes an acceptance of the terms". The court also upheld an order against the robot on trespass to chattels, on findings that it used a significant part of the system's capacity and that others would copy it. | Close. Daily automated collection of contact details from a public database, used to sell to the people listed. Differences: Verio made the sales contact itself; the database belonged to a private registrar; Verio admitted it knew the terms. Binds the Second Circuit. Of our states: NY. | Read in the opinion. Majority opinion read in full. The appendix, a draft opinion by Judge Parker, not read. |
| Nguyen v. Barnes & Noble Inc., 9th Cir., 2014, 763 F.3d 1171. [Link](https://www.courtlistener.com/opinion/2718526/kevin-nguyen-v-barnes-noble-inc/) | A shopper who never clicked to accept and never read the terms was not bound by terms linked at the foot of every page. A visible link alone does not give notice. But: "courts have consistently enforced browsewrap agreements where the user had actual notice of the agreement." | Not close on the facts: a consumer, a purchase and an arbitration clause. The rule is the point: what counts is whether the user knew of the terms or was given real notice. Binds the Ninth Circuit. | Read in the opinion. Read in full. |
| Meta Platforms, Inc. v. Bright Data Ltd., N.D. Cal., 2024. Details under question 1. | Meta's terms did not reach scraping of public data done while logged out, during or after the time Bright Data held accounts. The terms governed "use" by account holders. "Mere visitors without a Facebook or Instagram account do not see the Terms and are not asked to consent to them." Unclear wording was read against Meta, which wrote it. A clause said to survive the closing of an account did not ban scraping for ever. | Partly close. We hold no account, as far as this file knows. See question 22. The ruling turned on Meta's own wording. A portal with different wording could come out differently. Binds no other court. | Read in the opinion. |
| X Corp. v. Bright Data Ltd., N.D. Cal., 2024. Details under question 1. | The same court, a different judge, held the scraper bound by terms it had not clicked, because it knew them: "there is no question that Bright Data remained bound by the Terms, having impliedly agreed to them in ongoing scraping." The contract claim still failed in May 2024 for want of damage, and went forward in November 2024 once damage was pleaded. | Partly close. Bright Data was a large company that had held an account and had been told to stop. Binds no other court. | Read in the opinion. |
| hiQ Labs, Inc. v. LinkedIn Corp., N.D. Cal., order signed 2022-10-27, filed 2022-11-04, No. 3:17-cv-03301-EMC, docket entry 404. The Meta order cites it as 639 F. Supp. 3d 944; CourtListener could not confirm that citation. [Link](https://www.courtlistener.com/docket/6071320/404/hiq-labs-inc-v-linkedin-corporation/) | After the appeal, the trial court held that LinkedIn's agreement "unambiguously prohibits hiQ's scraping and unauthorized use of the scraped data", and that hiQ broke it, by scraping and by having contractors make false accounts. hiQ had accepted the agreement when it bought advertising and subscriptions, and had known of the ban since at least 2015. Whether LinkedIn had given up its right by doing nothing for years was left for trial. The docket shows a consent judgment and permanent injunction entered 2022-12-08. | Partly close. Shows that winning the CFAA point did not save hiQ from the contract claim. Difference: hiQ had accounts and had agreed to the terms. Binds no other court. | Read in the opinion. Pages 1 to 19 of 41 read: facts and breach of contract. The rest not read. The consent judgment not opened. |

**What the opinions say, taken together:**

1. On the CFAA, the opinions read treat data that anyone can see without a login differently from data behind a login. None of them is a final ruling by an appeal court on scraping of public data.
2. On contract, the opinions turn on whether the scraper knew the terms, not on whether it clicked. Register.com and X Corp. held a scraper that knew the terms to be bound. Meta held a logged-out scraper outside the terms, on Meta's wording.
3. On trespass to chattels, the opinions turn on load on the servers. X Corp. failed when it pleaded no harm and went forward when it pleaded server failures.
4. No opinion read concerns a government portal.

**Named in the opinions read, not opened.** These are pointers for the lawyer, not findings.

| Case, as cited in the opinion read | Where it was named |
|---|---|
| Intel Corp. v. Hamidi, 30 Cal. 4th 1342 (2003) | hiQ footnote 21 and both X Corp. orders, for the California rule that trespass to chattels needs harm |
| eBay, Inc. v. Bidder's Edge, Inc., 100 F. Supp. 2d 1058 (N.D. Cal. 2000) | hiQ footnote 21 |
| Sw. Airlines Co. v. FareChase, Inc., 318 F. Supp. 2d 435 (N.D. Tex. 2004) | hiQ footnote 21 |
| Facebook, Inc. v. Power Ventures, Inc., 844 F.3d 1058 (9th Cir. 2016) | hiQ, as a case about data behind a login |
| Specht v. Netscape Communications Corp., 306 F.3d 17 (2d Cir. 2002) | Register.com and Nguyen |
| Berman v. Freedom Financial Network, LLC, 30 F.4th 849 (9th Cir. 2022) | X Corp. order of May 2024 |
| EF Cultural Travel BV v. Zefer Corp., 318 F.3d 58 (1st Cir. 2003) | Fidlar |
| Compulife Software, Inc. v. Rutstein, 111 F.4th 1147 (11th Cir. 2024) | Came up in the question 1 search. Not opened. |

**For the lawyer:**

1. Have we read the terms of any portal? Register.com and X Corp. treat a scraper that knows the terms as bound by them. What follows for the portals whose terms we know, and should we read the rest?
2. Meta and X Corp. come from the same district and point different ways on a scraper with no account. Which view is a court in our states more likely to take?
3. The contract cases were brought by private companies that compete with the scraper or sell the same data. Does a city or county stand in the same place when it publishes records that the law requires it to keep open?

**Question 3 (state limits on commercial use of public records).**

| State | What the text says | Mark |
|---|---|---|
| Arizona, A.R.S. § 39-121.03 | A person who requests copies of public records for a commercial purpose must state that purpose. "Commercial purpose" includes "the sale of names and addresses to another for the purpose of solicitation". A person who obtains a record for a commercial purpose without stating it is liable for three times the fee that would have been charged, plus costs and attorney fees. | Read in the text |
| Washington, RCW 42.56.070(8) | Agencies may not "give, sell or provide access to lists of individuals requested for commercial purposes" unless a law specifically allows it. | Read in the text |
| Other 22 states and DC | | Not researched |

**For the lawyer:** both rules are written about a request made to an agency. The product reads open-data portals and makes no request. Do these rules reach us for Mesa, Tucson and Seattle? If they might, should those markets hide names or contact details?

**Case law on question 21 (do the Arizona and Washington rules reach data read from an open portal).**

**Nothing found on the question itself.** Every opinion found concerns a request made to an agency: whether the agency must hand the records over, or what it may charge. None concerns records an agency has already published on an open portal. None applies the Arizona penalty of three times the fee. The opinions below are recorded for what they say "commercial purpose" means.

| Case | What the court held | Our facts, and where it binds | Mark |
|---|---|---|---|
| Star Publishing Co. v. Parks, Ariz. Ct. App., 1993, 178 Ariz. 604, 875 P.2d 837. [Link](https://www.courtlistener.com/opinion/2612876/star-publishing-co-v-parks/) | A newspaper that asked for autopsy reports did not have a commercial purpose. The section is "aimed at the direct economic exploitation of public records not at the use of information gathered from public records in one's trade or business." The court added that "the reproduction of a public report or a group of public records for sale as such would be a commercial purpose." | Not close on how the records were obtained: a request to a county office. The second sentence quoted describes selling copies of records. Binds Arizona trial courts. | Read in the opinion. Read in full. |
| Primary Consultants, L.L.C. v. Maricopa County Recorder, Ariz. Ct. App., 2005, 210 Ariz. 393, 111 P.3d 435. [Link](https://www.courtlistener.com/opinion/2533690/primary-consultants-llc-v-maricopa-county-recorder/) | A political consulting firm run for profit did not have a commercial purpose when it used voter records in its work. The court read the definition as three parts: the use of a record for sale or resale; the obtaining of names and addresses for solicitation; and the sale of names and addresses to another, for solicitation or for the buyer's gain. The closing words about "monetary gain" are not a catch-all. They apply only to the sale of names and addresses. | Not close on how the records were obtained: requests to the county. The third part of the definition describes selling names and addresses to a buyer who expects to gain from them. Binds Arizona trial courts. | Read in the opinion. Read in full. |
| LaWall v. R.R. Robertson, L.L.C., Ariz. Ct. App., 2015, 237 Ariz. 495, 353 P.3d 375. [Link](https://www.courtlistener.com/opinion/6604313/lawall-v-rr-robertson-llc/) | An investigations firm asked for prosecution records to fill a database it used for paid sentencing studies. The requests fell inside the exception for "research for evidence in an action in any judicial or quasi-judicial body", so the higher commercial charges did not apply. Footnote 13 notes that the statute gives a records office no way to test a request, short of going to court, before the record is handed over. | Not close. The case turned on an exception for court evidence that has nothing to do with sales leads. Binds Arizona trial courts. | Read in the opinion. Read in full. |
| SEIU Healthcare 775NW v. Department of Social & Health Services, Wash. Ct. App., 2016, 193 Wash. App. 377, 377 P.3d 214. The opinion text ends "Review denied at 186 Wn.2d 1016 (2016)". [Link](https://www.courtlistener.com/opinion/7035464/seiu-healthcare-775nw-v-department-of-social-health-services/) | The first Washington court ruling on the term, by its own account. "Commercial purposes" "includes a business activity by any form of business enterprise intended to generate revenue or financial benefit", and the requester must mean to profit from direct use of the list. A non-profit that wanted a list of care providers to tell them of their rights did not have a commercial purpose. The rule is addressed to the agency, which must look into a request when it has some sign the list may be used commercially. The opinion reports a 1975 Attorney General opinion that found a commercial purpose where a list was requested in order to sell it, or to find people to sell to. | Not close on how the records were obtained: a request to a state agency, and the question was whether the agency could release the list. Binds Washington trial courts. | Read in the opinion. Read in full. |

Searches run on CourtListener, opinions only:

| Search | Result |
|---|---|
| "39-121.03" and "commercial purpose" | 5 results. Three read, above. State v. Ross (Ariz. Ct. App. 2007) not opened. The fifth is a second copy of LaWall. |
| "lists of individuals" and "commercial purposes" with "42.56.070" or "42.17.260" | 10 results. SEIU Healthcare read. The others not opened, among them In re Rosier (Wash. 1986), Tacoma Public Library v. Woessner (Wash. Ct. App. 1998), SEIU Local 925 v. Freedom Foundation (Wash. Ct. App. 2016) and a 1998 Attorney General opinion. |

**For the lawyer:**

1. SEIU Healthcare cites the Washington rule as RCW 42.56.070(9). This file cites it as (8), from the state's site on 2026-09-29. Please confirm the current number.
2. If these rules do reach open-portal data, the meaning given to "commercial purpose" in Primary Consultants and in the 1975 Attorney General opinion includes selling names to a buyer who will use them to sell. Is a permit lead within that?

## Group 2: contact details of people on permits

**Question 4 and 5 (showing contact details; companies and individuals).** See the finding above. No statute read so far forbids showing a detail taken from a government record to a paying customer. That is not the same as a statute allowing it, and 15 states have not been researched for anything beyond the privacy law list below.

The company and individual distinction does appear in the law: Vermont's exclusion covers only business or professional information, and the federal calling rules below treat a business line and a home or mobile line differently.

**Question 6 (state privacy laws; data broker registration).**

Comprehensive privacy laws in the market states:

| State | Who it applies to | Mark |
|---|---|---|
| California | Revenue over $25,000,000; or personal information of 100,000 or more consumers or households; or 50 percent or more of revenue from selling or sharing personal information. § 1798.140(d) | Read in the text |
| Virginia | Personal data of 100,000 consumers; or 25,000 consumers and over 50 percent of revenue from selling personal data. § 59.1-576 | Read in the text |
| Texas | Any business that is not a small business under the federal Small Business Administration definition. No consumer count. § 541.002 | Read in the text (unofficial site) |
| Oregon | Threshold not read. Definitions read. | Partly read |
| Nebraska | No consumer count; small businesses excluded. In force 2025-01-01. | Commentary only |
| Tennessee | Revenue over $25,000,000 and 175,000 consumers. In force 2025-07-01. | Commentary only |
| Minnesota | 100,000 consumers; or 25,000 and 25 percent of revenue from selling data. In force 2025-07-31. | Commentary only |
| Maryland | 35,000 consumers. In force 2025-10-01. | Commentary only |
| Kentucky | 100,000 consumers; or 25,000 and 50 percent of revenue from selling data. In force 2026-01-01. | Commentary only |
| Oklahoma | 100,000 consumers; or 25,000 and over 50 percent of revenue from selling data. Signed 2026-03-20. In force 2027-01-01. | Commentary only |
| Colorado | | Not researched |
| AZ, DC, FL, GA, IL, LA, MA, MI, MO, NC, NY, OH, PA, WA | Whether a comprehensive law exists. | Not researched |

Data broker registration:

| State | Registration | Mark |
|---|---|---|
| California | Yearly, in January. Fee reported as $6,600. From 2026-08-01 a registered broker must collect deletion requests from the state's DROP system at least every 45 days. Fine for not registering reported as $200 a day. | Commentary only |
| Texas | Applies to a business "whose principal source of revenue" comes from personal data it did not collect from the individual. § 509.001 | Read in the text (unofficial site) |
| Oregon | Registration before collecting or selling brokered personal data in the state. Penalty up to $500 per violation, $10,000 a year. ORS 646A.593 | Read in the text (unofficial site) |
| Vermont | Registration section not read. | Partly read |

**For the lawyer:** if the government records exclusion holds, the product appears to fall outside the California, Texas and Oregon data broker definitions. Please confirm or correct. The California third threshold (50 percent of revenue) would otherwise be met by a company of any size.

**Question 7 (removal).**

No general removal right was found for information from government records, because the privacy laws above exclude it. Two kinds of law give a removal right anyway:

| Law | What it says | Mark |
|---|---|---|
| Federal Daniel Anderl Judicial Security and Privacy Act | A data broker may not knowingly sell or transfer covered information of a federal judge or the judge's immediate family. A business that receives a written request must remove the information within 72 hours. Its data broker definition covers a business that collects personal information on people who are not its customers "in order to sell the information or otherwise profit from providing third party access". | Commentary only |
| State laws for judges, police and other officials | New Jersey (removal within 10 days, private lawsuits), Maryland, Illinois, Minnesota and others. Reported in 13 to 15 states. | Commentary only |

**For the lawyer:** a permit shows the property address beside the owner's name. For a homeowner that is a home address, with a phone number where the record has one. The federal definition of data broker has no government records exclusion in the wording found. Do we need a removal process for this reason alone, and what response time?

## Group 3: how customers use the data

**Question 8 (calls and texts).**

| Rule | What the text says | Mark |
|---|---|---|
| FCC rule, 47 C.F.R. § 64.1200(a)(1)(iii) | No call using an automatic dialling system or a prerecorded voice to a number assigned to a cellular service, without consent. Telemarketing calls of that kind need prior express written consent. No exception for business calls was found in the summary read. | Read in the text (summary, not full quotation) |
| FCC rule, 47 C.F.R. § 64.1200(c)(2) | No telephone solicitation to "a residential telephone subscriber who has registered his or her telephone number on the national do-not-call registry". Paragraph (e) extends the rules to wireless numbers. | Read in the text (summary) |
| FTC Telemarketing Sales Rule, 16 C.F.R. § 310.6(b)(7) | Calls "between a telemarketer and any business" to sell to the business are exempt, except the rules against misrepresentation in § 310.3(a)(2) and (4). | Read in the text |
| FTC Telemarketing Sales Rule, 16 C.F.R. § 310.3(b) | "It is a deceptive telemarketing act or practice and a violation of this part for a person to provide substantial assistance or support to any seller or telemarketer when that person knows or consciously avoids knowing that the seller or telemarketer is engaged in any act or practice that violates §§ 310.3(a), (c) or (d), or § 310.4 of this part." | Read in the text |
| FTC guidance on the rule | Assistance must be "more than just a casual or incidental dealing". "Taking deliberate steps to ensure one's own ignorance" does not avoid liability. | Read on ftc.gov |
| FTC enforcement | The FTC has used § 310.3(b) against lead sellers: Response Tree (2024) and MediaAlpha ($45 million, 2025). Both cases concerned consumer leads and robocalls, not business leads. | Commentary only |
| Florida, Fla. Stat. § 501.059 | Covers calls and texts "to a consumer" to sell "consumer goods or services", meaning property "normally used for personal, family, or household purposes". Automated calls need prior express written consent. A called party may recover $500 per call, up to three times that if wilful. | Read in the text |
| Texas | Marketing texts count as telephone solicitations since 2025-09-01. Damages reported as $500 to $5,000. | Commentary only |
| Tennessee, Virginia, Arizona, Maryland, Washington, Oklahoma | Have their own calling or texting rules. | Commentary only |

**What this means for the brief:**

1. A customer selling fire protection work to a company is mostly inside the business exemption of the FTC rule. The FCC rule on automatic dialling to mobile numbers has no such exemption in what was read.
2. A homeowner who pulled their own permit is a consumer. Calls to that person fall under the Do Not Call registry and state laws like Florida's.
3. The company can be liable for a customer's calls under § 310.3(b), but only if it knows, or avoids knowing, that the customer is breaking the rule. This is the strongest reason found for the terms to say how customers may contact people, and for acting when a complaint arrives.

**Case law on question 8.**

Cases about consumer leads. Every case found is of this kind.

| Case | What the court held | Our facts, and where it binds | Mark |
|---|---|---|---|
| Federal Trade Commission v. Chapman, 10th Cir., 2013, 714 F.3d 1211. [Link](https://www.courtlistener.com/opinion/866846/federal-trade-commission-v-chapman/) | Upheld a judgment of $1,682,950 and an injunction under 16 C.F.R. § 310.3(b) against a contractor who supplied the grant research that telemarketers sold with false claims. The help need not be tied to the false statements themselves. It must be more than "casual or incidental". The court said "actual knowledge is not necessary under the 'conscious avoidance' standard." The signs she passed over included inquiries by state attorneys general, complaints, and a sales script she was sent and chose not to read. | Not close. Consumer fraud. She supplied the very thing the callers sold, and they were 80 to 90 percent of her business. It shows how a court applies the "knows or consciously avoids knowing" test. Binds the Tenth Circuit. Of our states: CO, OK. | Read in the opinion. Read in full. |
| Federal Trade Commission v. Day Pacer LLC, 7th Cir., decided 2025-01-03, Nos. 23-3310, 24-1273 and 24-1289. No reporter citation found. [Link](https://www.courtlistener.com/opinion/10307189/ftc-v-day-pacer-llc/) | Lead sellers were liable under the Telemarketing Sales Rule for about 3.7 million calls to numbers on the Do Not Call registry, though they sold nothing on the calls. They bought contact details from websites, called to ask about interest in schooling, and sold the leads to schools. "Telemarketing" covers a "plan, program, or campaign" to bring about a sale. Consent a person gave to the website did not pass to the caller. The companies were also liable for helping a calling partner. The penalty of $28.6 million was sent back to be worked out again. | Not close in the main point: Day Pacer made the calls itself and paid others to call. The company makes no calls. Close in one point: a lead seller's defence that it sells nothing by phone failed. Consumer leads. The trial court had paused its order so far as it barred calls to other businesses. Binds the Seventh Circuit. Of our states: IL. | Read in the opinion. Read in full. |
| Kristensen v. Credit Payment Services Inc., 9th Cir., 2018, 879 F.3d 1010. [Link](https://www.courtlistener.com/opinion/4458415/flemming-kristensen-v-credit-payment-services-inc/) | Under the Telephone Consumer Protection Act (TCPA), liability for another's calls or texts follows the ordinary law of agency. Lenders and a lead dealer were not liable for unlawful texts sent by a lead publisher they had no contract or contact with. The one company that had hired the publisher was not liable either, because it did not know of the unlawful texts and had seen nothing that called for a closer look. "The knowledge that an agent is engaged in an otherwise commonplace marketing activity is not the sort of red flag that would lead a reasonable person to investigate". | The reverse of our position. The question there was whether those who buy leads answer for the one who sends the texts. With us the customer makes the call, and the question would be whether the customer acts for the company. Consumer loan leads. Binds the Ninth Circuit. The court relied on rulings of the FCC; see "Whether the cases still stand". | Read in the opinion. Read in full. |

Cases about business-to-business data: **nothing found.** No opinion was found that holds a seller of business contact data or permit data liable, or not liable, for calls or texts its customers made.

Found but not read:

| Case | Why it is listed |
|---|---|
| Chennette v. Porch.com, Inc., 9th Cir., No. 20-35962, opinion filed 2022-10-12, "reversed and remanded" by the docket. Hall v. Smosh Dot Com, Inc., 72 F.4th 983 (9th Cir. 2023), cites it as 50 F.4th 1217. [Docket](https://www.courtlistener.com/docket/66991035/nathan-chennette-v-porchcom-inc/) | The opinion text is not on CourtListener, and the citation did not resolve there. It was looked for in the belief that it concerns sales texts sent to the mobile phones of home improvement contractors. That belief comes from memory and is not checked. If it is right, this is the case nearest to our customers' use, and the lawyer should read it first. |
| United States v. DISH Network L.L.C., 954 F.3d 970 (7th Cir. 2020) | Named in Day Pacer on the size of penalties. Came up in the search on § 310.3(b). Not opened. |
| Federal Trade Commission v. Pukke, 53 F.4th 80 (4th Cir. 2022), and Federal Trade Commission v. Universal Processing Services of Wisconsin, LLC, 877 F.3d 1234 (11th Cir. 2017) | Came up in the search on § 310.3(b). Not opened. |

Searches run on CourtListener, opinions only:

| Search | Result |
|---|---|
| Meaning search: "Can a company that sells leads or telephone number lists be liable under the Telephone Consumer Protection Act for calls placed by its customers?" | First 20 results looked at by name. Day Pacer read. None of the others, by name, is a suit against a seller of data who made no calls. |
| "substantial assistance" and "Telemarketing Sales Rule" with "310.3(b)" or "consciously avoid", federal appeal courts | 13 results. Chapman and Day Pacer read. |
| "Telephone Consumer Protection Act" with lead seller terms, "vicarious liability" or "did not initiate", and "business-to-business" or "business number" or "business line" | 0 results |
| "Porch.com" with "Telephone Consumer Protection Act" and "home improvement" | 0 results |

State calling laws (Florida, Texas and the others in the table above): case law not searched.

**Question 9 (email).**

| Rule | What the text says | Mark |
|---|---|---|
| CAN-SPAM, 15 U.S.C. § 7704(a)(5) | A commercial email must identify itself as an advertisement, give notice of how to opt out, and carry the sender's valid postal address. | Read in the text |
| CAN-SPAM, 15 U.S.C. § 7704(a)(4)(A) | The sender must stop within 10 business days of an opt-out request. | Read in the text |
| CAN-SPAM, 15 U.S.C. § 7704(b)(1) | It is unlawful to send an email that breaks subsection (a), "or to assist in the origination of such message through the provision or selection of addresses", knowing that the address "was obtained using an automated means from an Internet website" that carried a notice that its operator will not give or sell addresses for the purpose of sending email. | Read in the text |

**For the lawyer:** § 7704(b)(1) names the party that provides the addresses. It applies only where the email itself is unlawful and the website carried the notice. Should we check each of the 44 sources for such a notice, and hide email addresses from any source that has one?

**Case law on question 9.**

**Nothing found on § 7704(b)(1).** No opinion was found that applies it to a party that supplied the addresses, or that says what kind of website notice is enough.

| Case | What the court held | Our facts, and where it binds | Mark |
|---|---|---|---|
| Gordon v. Virtumundo, Inc., 9th Cir., 2009, 575 F.3d 1040. [Link](https://www.courtlistener.com/opinion/1214526/gordon-v-virtumundo-inc/) | A person who receives unlawful commercial email cannot sue under CAN-SPAM. "Congress conferred standing only on a narrow group of possible plaintiffs: the Federal Trade Commission, certain state and federal agencies, state attorneys general, and IAS providers adversely affected by violations of the CAN-SPAM Act." An "IAS provider" is a provider of internet access service, and it must show real harm of the kind such providers suffer. Footnote 7 says § 7704(b) sets out "aggravated violations" such as "e-mail harvesting", which were "not at issue in this lawsuit". | Not close. A man who collected spam sued email marketers. It bears on who could bring a claim under § 7704(b)(1): not the person emailed. Binds the Ninth Circuit. | Read in the opinion. Read from the start into part III.B.3, about the first third. The rest of the standing section, the part on state law, and the concurrence not read. |

Searches run on CourtListener, opinions only:

| Search | Result |
|---|---|
| "CAN-SPAM" with "7704(b)(1)" or "address harvesting" or "dictionary attack" or "harvested" | 4 results: MySpace, Inc. v. Wallace (C.D. Cal. 2007), Gordon twice, United States v. Simpson (5th Cir. 2015). Searched inside each for "7704(b)". MySpace deals with § 7704(b)(2), automated sign-up for accounts, and found the evidence too thin. None applies (b)(1). |
| "CAN-SPAM" with "assist in the origination" or "provision or selection of addresses" or "harvest" or "harvesting" | 10 results. Searched inside four for "harvest": Martin v. CCH, Inc. (N.D. Ill. 2011), Facebook, Inc. v. Power Ventures, Inc. (N.D. Cal. 2012), Unspam Technologies, Inc. v. Chernuk (4th Cir. 2013) and Gordon. None applies (b)(1). Beyond Systems, Inc. v. Kraft Foods, Inc. (D. Md. 2013) could not be searched: the request was refused for rate limit. |

Not searched: FTC and state attorney general enforcement actions that ended in a settlement. They do not appear as opinions.

**Questions 10 and 11 (terms, export limits).** Not researched. These are drafting questions for the lawyer. The findings under questions 8 and 9 bear on them.

## Groups 4 and 5

Questions 12 to 16 are not researched. They are contract and drafting questions.

Two points for the documents, from reading the live pages:

1. The privacy page says permit data is "not personal information about our users". That is true and misses the point: it is information about other people, and the page says nothing to them.
2. The privacy page says "We do not sell your personal information." The sentence is about customers. The lawyer should say whether it needs a second sentence on permit records, which the company does sell access to.

## Whether the cases still stand

Checked on CourtListener on 2026-09-29. Three checks were made:

1. **Citation check.** Each reporter citation was looked up to confirm it leads to the case named. All did, except the two marked below.
2. **Later opinions.** A search for later opinions, from the same court or a higher one, that cite the case and use the words "overruled", "abrogated" or "superseded".
3. **Reading the hits.** The newest hits were searched inside to see how the case was used.

These checks can miss a later case. No check was made on whether Congress or a state has changed a statute since a case was decided.

| Case | What was found |
|---|---|
| Van Buren (U.S. 2021) | Citation confirmed. Cited by the Supreme Court in Fischer v. United States, 603 U.S. 480 (2024), as support on how to read a criminal statute. No sign of being overruled. |
| hiQ v. LinkedIn (9th Cir. 2022) | Citation confirmed. Cited by the Ninth Circuit in United States v. Sullivan (2025) for its CFAA rule, and in two 2026 opinions for the test for an early injunction. No sign of being overruled. It replaced the 2019 opinion, 938 F.3d 985, which the Supreme Court had set aside. The case ended in a consent judgment against hiQ, so the CFAA point never had a final ruling. |
| Fidlar v. LPS (7th Cir. 2016) | Citation confirmed. No later Seventh Circuit or Supreme Court opinion citing it was found. Decided before Van Buren. Could not check further. |
| Register.com v. Verio (2d Cir. 2004) | Citation confirmed. Cited with approval by the Second Circuit in Sudakow v. CleanChoice Energy, Inc. (2025). No sign of being overruled. |
| Nguyen v. Barnes & Noble (9th Cir. 2014) | Citation confirmed. Cited with approval by the Ninth Circuit in Platt v. Sodexo, S.A. (2025) and Heckman v. Live Nation Entertainment, Inc., 120 F.4th 670 (2024). No sign of being overruled. |
| FTC v. Chapman (10th Cir. 2013) | Citation confirmed. No later Tenth Circuit or Supreme Court opinion citing it was found. Could not check further. |
| FTC v. Day Pacer (7th Cir. 2025) | No reporter citation yet on CourtListener. No opinion citing it was found. Whether a rehearing or Supreme Court review was sought was not checked. |
| Kristensen v. Credit Payment Services (9th Cir. 2018) | Citation confirmed. No later Ninth Circuit or Supreme Court opinion was found that questions it. It rests in part on the court deferring to FCC rulings. The lawyer should check whether later Supreme Court decisions on deference to agencies change that. |
| Gordon v. Virtumundo (9th Cir. 2009) | Citation confirmed. Cited by the Ninth Circuit in Langer v. Kiser, 57 F.4th 1085 (2023), on another point. No sign of being overruled. |
| Star Publishing, Primary Consultants, LaWall (Ariz. Ct. App.) | Citations confirmed. LaWall (2015) relies on the other two. Two opinions came up in the search. Judicial Watch Inc. v. Mayes (Ariz. Ct. App. 2026) does not mention § 39-121.03. Valerie M. v. Arizona Department of Economic Security, 219 Ariz. 155 (App. 2008), was not opened; LaWall is later and still relies on both earlier cases. No sign of being overruled. |
| SEIU Healthcare 775NW v. DSHS (Wash. Ct. App. 2016) | Citation confirmed. The opinion text records that review was denied. Followed on the meaning of "commercial" in Blue Ribbon Farms Property Owners' Association v. Mason (Wash. Ct. App. 2024), a case about a land covenant. No sign of being overruled. |
| Meta v. Bright Data, X Corp. v. Bright Data, hiQ v. LinkedIn order of 2022 (N.D. Cal.) | Trial court orders. They bind no other court. None has a reporter citation on CourtListener. The citation 639 F. Supp. 3d 944, given for the hiQ order in the Meta order, did not resolve on CourtListener. Whether either Bright Data case was appealed was not checked. |
| Chennette v. Porch.com (9th Cir. 2022) | Not read. The citation 50 F.4th 1217 did not resolve on CourtListener. |

## Questions to add to the brief

These came out of the research and are not in the sixteen.

17. Does the government records exclusion survive our copying, scoring and resale?
18. Do laws that protect judges, police and other officials require us to remove a person on request, and how fast?
19. Must we check each portal for a notice against passing on email addresses (CAN-SPAM § 7704(b)(1))?
20. Should the terms forbid using the data to decide on credit, insurance, employment or housing, so that the Fair Credit Reporting Act does not apply to us? Not researched.
21. Do Arizona's and Washington's commercial purpose rules reach data read from an open portal?

These came out of the case law.

22. Does the scraper hold an account, an API key or an app token on any portal? If it does, terms were probably accepted when it was made. hiQ lost on contract because it had accepted LinkedIn's terms. Which of our sources are in that position?
23. Register.com and X Corp. treat a scraper that knows a site's terms as bound by them, click or no click. Are we bound by the terms of portals whose terms we have read? Should we read the terms of all 44 sources?
24. Trespass to chattels turns on load on the server (Register.com; X Corp., November 2024). What request rate and timing should the scraper keep to, and should we keep a record of it?
25. In Register.com the terms forbade using the data to solicit the people listed, and the court enforced that. Do any of our portals' terms forbid solicitation or commercial use? If so, should those sources be dropped, or the limit passed on to customers?
26. In Day Pacer, consent a person gave to the source of the data did not pass to the caller. Should the terms tell customers that a lead gives them no consent to call, text or email anyone?
27. In Chapman, complaints and regulators' inquiries that went unexamined became "conscious avoidance". What should we do, and what should we record, when a complaint about a customer's calls reaches us?
28. Under Gordon, the person emailed cannot sue under CAN-SPAM. Who could bring a claim under § 7704(b)(1) against a company that supplied the addresses: the FTC, a state attorney general, an email provider?

## Still to do

- Fact 8 is filled (2026-09-29): no company formed; the owner works from Houston, Texas. Texas federal courts follow the Fifth Circuit, and Texas state courts follow Texas law. No opinion in this file comes from the Fifth Circuit or a Texas state court, so none of the cases read binds a court where the owner lives. Sw. Airlines Co. v. FareChase, Inc. (N.D. Tex. 2004), named under question 2 and not opened, is a Texas federal trial court order.
- Run every case in this file through KeyCite or Shepard's. The checks made here are weaker.
- Read Chennette v. Porch.com (9th Cir. 2022) from another source. It could not be read on CourtListener.
- Case law from the circuits not covered for questions 1 and 2: First, Third, Fourth, Fifth, Sixth, Eighth, Eleventh and D.C. The Fifth Circuit and Texas come first, because the owner is in Texas.
- Case law on the state calling and texting laws under question 8.
- Case law for question 18, and First Amendment and Driver's Privacy Protection Act cases for questions 17 and 18.
- Open the cases listed as named or found but not opened, where the lawyer thinks them worth it.
- Read the terms of use of each of the 44 sources, once the lawyer has answered question 23.
- Read the statutes behind every "Commentary only" row.
- Public records rules for the 22 states and DC not yet researched.

## Sources

Read in the text:

- 16 C.F.R. § 310.3: https://www.law.cornell.edu/cfr/text/16/310.3
- 16 C.F.R. § 310.6: https://www.law.cornell.edu/cfr/text/16/310.6
- 47 C.F.R. § 64.1200: https://www.law.cornell.edu/cfr/text/47/64.1200
- 15 U.S.C. § 7704: https://www.law.cornell.edu/uscode/text/15/7704
- Cal. Civ. Code § 1798.140: https://leginfo.legislature.ca.gov/faces/codes_displaySection.xhtml?lawCode=CIV&sectionNum=1798.140
- Cal. Civ. Code § 1798.99.80: https://leginfo.legislature.ca.gov/faces/codes_displaySection.xhtml?lawCode=CIV&sectionNum=1798.99.80
- Tex. Bus. & Com. Code §§ 509.001 to 509.003, § 541.002: https://texas.public.law/statutes/tex._bus._and_com._code_section_509.001
- ORS 646A.593: https://oregon.public.law/statutes/ors_646a.593
- ORS 646A.570: https://oregon.public.law/statutes/ors_646a.570
- 9 V.S.A. § 2430: https://legislature.vermont.gov/statutes/section/09/062/02430
- Va. Code § 59.1-575: https://law.lis.virginia.gov/vacode/title59.1/chapter53/section59.1-575/
- Va. Code § 59.1-576: https://law.lis.virginia.gov/vacode/title59.1/chapter53/section59.1-576/
- A.R.S. § 39-121.03: https://www.azleg.gov/ars/39/00121-03.htm
- RCW 42.56.070: https://app.leg.wa.gov/RCW/default.aspx?cite=42.56.070
- Fla. Stat. § 501.059: http://www.leg.state.fl.us/statutes/index.cfm?App_mode=Display_Statute&URL=0500-0599/0501/Sections/0501.059.html
- FTC, Complying with the Telemarketing Sales Rule: https://www.ftc.gov/business-guidance/resources/complying-telemarketing-sales-rule

Read in the opinion, all on CourtListener:

- Van Buren v. United States, 593 U.S. 374 (2021): https://www.courtlistener.com/opinion/4888668/van-buren-v-united-states/
- hiQ Labs, Inc. v. LinkedIn Corp., 31 F.4th 1180 (9th Cir. 2022): https://www.courtlistener.com/opinion/6460342/hiq-labs-inc-v-linkedin-corporation/
- hiQ Labs, Inc. v. LinkedIn Corp., N.D. Cal., docket entry 404 (2022): https://www.courtlistener.com/docket/6071320/404/hiq-labs-inc-v-linkedin-corporation/
- Fidlar Technologies v. LPS Real Estate Data Solutions, Inc., 810 F.3d 1075 (7th Cir. 2016): https://www.courtlistener.com/opinion/8442520/fidlar-technologies-v-lps-real-estate-data-solutions-inc/
- Meta Platforms, Inc. v. Bright Data Ltd., N.D. Cal., docket entry 181 (2024): https://www.courtlistener.com/docket/66706470/181/meta-platforms-inc-v-bright-data-ltd/
- X Corp. v. Bright Data Ltd., N.D. Cal., docket entry 83 (2024): https://www.courtlistener.com/docket/67637345/83/x-corp-v-bright-data-ltd/
- X Corp. v. Bright Data Ltd., N.D. Cal., docket entry 156 (2024): https://www.courtlistener.com/docket/67637345/156/x-corp-v-bright-data-ltd/
- Register.com, Inc. v. Verio, Inc., 356 F.3d 393 (2d Cir. 2004): https://www.courtlistener.com/opinion/784889/registercom-inc-v-verio-inc/
- Nguyen v. Barnes & Noble Inc., 763 F.3d 1171 (9th Cir. 2014): https://www.courtlistener.com/opinion/2718526/kevin-nguyen-v-barnes-noble-inc/
- Federal Trade Commission v. Chapman, 714 F.3d 1211 (10th Cir. 2013): https://www.courtlistener.com/opinion/866846/federal-trade-commission-v-chapman/
- Federal Trade Commission v. Day Pacer LLC (7th Cir. 2025): https://www.courtlistener.com/opinion/10307189/ftc-v-day-pacer-llc/
- Kristensen v. Credit Payment Services Inc., 879 F.3d 1010 (9th Cir. 2018): https://www.courtlistener.com/opinion/4458415/flemming-kristensen-v-credit-payment-services-inc/
- Gordon v. Virtumundo, Inc., 575 F.3d 1040 (9th Cir. 2009): https://www.courtlistener.com/opinion/1214526/gordon-v-virtumundo-inc/
- Star Publishing Co. v. Parks, 178 Ariz. 604 (App. 1993): https://www.courtlistener.com/opinion/2612876/star-publishing-co-v-parks/
- Primary Consultants, L.L.C. v. Maricopa County Recorder, 210 Ariz. 393 (App. 2005): https://www.courtlistener.com/opinion/2533690/primary-consultants-llc-v-maricopa-county-recorder/
- LaWall v. R.R. Robertson, L.L.C., 237 Ariz. 495 (App. 2015): https://www.courtlistener.com/opinion/6604313/lawall-v-rr-robertson-llc/
- SEIU Healthcare 775NW v. Department of Social & Health Services, 193 Wash. App. 377 (2016): https://www.courtlistener.com/opinion/7035464/seiu-healthcare-775nw-v-department-of-social-health-services/

Searched inside, not read in full. Used only for the good-law checks and the "nothing found" entries:

- Fischer v. United States, 603 U.S. 480 (2024): https://www.courtlistener.com/opinion/9986255/fischer-v-united-states/
- United States v. Sullivan (9th Cir. 2025): https://www.courtlistener.com/opinion/10734992/united-states-v-sullivan/
- Sudakow v. CleanChoice Energy, Inc. (2d Cir. 2025): https://www.courtlistener.com/opinion/10661587/sudakow-v-cleanchoice-energy-inc/
- Platt v. Sodexo, S.A. (9th Cir. 2025): https://www.courtlistener.com/opinion/10647075/robert-platt-v-sodexo-sa/
- Heckman v. Live Nation Entertainment, Inc., 120 F.4th 670 (9th Cir. 2024): https://www.courtlistener.com/opinion/10162317/skot-heckman-v-live-nation-entertainment-inc/
- Langer v. Kiser, 57 F.4th 1085 (9th Cir. 2023): https://www.courtlistener.com/opinion/9369813/chris-langer-v-milan-kiser/
- Hall v. Smosh Dot Com, Inc., 72 F.4th 983 (9th Cir. 2023): https://www.courtlistener.com/opinion/9410789/kristen-hall-v-smosh-dot-com-inc/
- Judicial Watch Inc. v. Mayes (Ariz. Ct. App. 2026): https://www.courtlistener.com/opinion/10850265/judicial-watch-inc-v-kristen-mayes/
- Blue Ribbon Farms Property Owners' Association v. Mason (Wash. Ct. App. 2024): https://www.courtlistener.com/opinion/9498212/blue-ribbon-farms-property-owners-association-v-michael-marilyn-mason/
- MySpace, Inc. v. Wallace, 498 F. Supp. 2d 1293 (C.D. Cal. 2007): https://www.courtlistener.com/opinion/1652277/myspace-inc-v-wallace/
- State of Texas v. Arity 875, LLC (Tex. App. 15th Dist. 2025): https://www.courtlistener.com/opinion/10651269/state-of-texas-v-arity-875-llc/

Commentary:

- State privacy law thresholds: https://secureprivacy.ai/blog/us-state-privacy-law-tracker-2026
- Privacy laws taking effect in 2026: https://www.multistate.us/insider/2026/2/4/all-of-the-comprehensive-privacy-laws-that-take-effect-in-2026
- Oklahoma SB 546: https://www.hunton.com/privacy-and-cybersecurity-law-blog/oklahoma-enacts-comprehensive-consumer-privacy-law
- California Delete Act and DROP: https://www.coblentzlaw.com/news/navigating-californias-data-broker-requirements-in-2026/
- FTC and lead sellers: https://www.venable.com/insights/publications/2023/07/ftc-settlements-with-lead-generators-offer
- FTC and Response Tree: https://www.ftc.gov/news-events/news/press-releases/2024/01/california-based-lead-generator-agrees-settlement-banning-it-making-or-assisting-others-making
- State calling and texting laws: https://www.goodwinlaw.com/en/insights/publications/2026/03/insights-finance-cfs-yir-telephone-consumer-protection-act
- Texas texts: https://www.kelleydrye.com/viewpoints/blogs/ad-law-access/texas-mini-tcpa-law-faqs-for-marketing-texts
- Daniel Anderl Act text: https://www.congress.gov/bill/117th-congress/senate-bill/2340/text
- State laws for judges and officials: https://www.ncsl.org/civil-and-criminal-justice/address-protections-for-public-officials-and-employees
