# Legal review: to do before customers use contact details

**Status: not done.** Owner's note, 2026-09-29: talk to a lawyer about the legality of the product before paying customers start calling or emailing the people on permits.

This file is a brief for that conversation. It was not written by a lawyer and is not legal advice. It lists facts and questions, not answers.

## Why now

Since 2026-09-28 a lead shows the phone number, email address and licence number of the parties on a permit, where the government record itself publishes them. Since 2026-09-29 the CSV export carries them too. Customers will use them for sales calls and sales email. How that contact is made is regulated, whoever published the number.

## Facts to give the lawyer

Send these first. They decide most of the answers.

1. **What the product is.** A paid subscription for fire-protection contractors in the United States. It lists recent building and fire permits as sales leads, with a score.
2. **Where the data comes from.** Public permit records on city and county open-data portals and permit websites, in 32 US markets. They are read automatically, once a day, without logging in.
3. **What personal data it holds about people who are not customers.** Names of owners, applicants and contractors. For some cities also a phone number, an email address and a contractor licence number. Some of these are individuals, not companies, and some phone numbers are likely personal mobile numbers.
4. **What it does not do.** It does not look anyone up anywhere else, buy data, or add to what the record publishes. It does not show a party's mailing address.
5. **Who sees it.** Signed-in, paying subscribers, for the markets on their plan. Contact details are never on a public page.
6. **What customers can do with it.** Read it on screen and download it as a CSV file, up to 5,000 leads at a time.
7. **What the company itself does not do.** It does not call, text or email the people on permits.
8. **Where the company is.** Fill in: state of formation, state of operation, and whether any customer or any person in the data is outside the United States.
9. **What the terms say today.** No guarantee of accuracy; no resale or sharing of exported data outside the customer's organisation. Nothing on how customers may contact people, and nothing on a person asking to be removed.

## Questions to ask

Ask for a yes, no or "it depends on" for each, and for the wording to add where wording is the fix.

### 1. Collecting and reselling public records

1. May we collect permit records from government portals automatically and sell access to them? Does the answer change by state or by portal?
2. Some portals have terms of use. Are we bound by terms we never clicked to accept? Which kinds of clause should stop us using a portal?
3. Do any of our states restrict commercial use of public records, or of names and addresses taken from them?

### 2. Contact details of people on permits

4. Is showing a phone number or email address from a public permit record to paying customers lawful in every state we cover?
5. Does it matter whether the party is a company or an individual, such as a homeowner who pulled their own permit? Should we hide details of individuals?
6. Do state privacy laws (California, and the newer state laws) apply to us as a business that sells personal information? Do we have to register as a data broker anywhere?
7. Must we offer people a way to have their details removed? What must the process be, and how fast?

### 3. How customers use the data

8. If a customer breaks the rules on sales calls or texts (TCPA, the national and state Do Not Call lists) using a number from our product, can we be held liable?
9. The same question for sales email (CAN-SPAM).
10. What should our terms require of customers so that responsibility sits with them? Is a clause enough, or do we need to do more, such as marking mobile numbers or checking Do Not Call lists?
11. Should we limit or log exports of contact details?

### 4. What we promise

12. Our marketing says "leads" and shows a score. Is there any claim we should not make, given that records can be late, wrong or amended?
13. Is our "no guarantee of accuracy" wording enough if a customer acts on a wrong record?
14. Do our limits on liability and our refund terms hold up for a subscription sold to businesses?

### 5. Documents

15. Please review the terms of service and the privacy policy as they stand. What is missing?
16. Does the privacy policy need a section for people who appear in permit records but are not customers?

## What to bring

- The live terms and privacy pages: `permittorch.com/terms` and `permittorch.com/privacy`.
- A screenshot of one lead page with contact details, and one exported CSV file.
- The list of markets and sources: `docs/superpowers/plans/2026-08-19-permittorch-mvp/scraper-source-registry.json`.
- The decisions on contact details: section 08 of `docs/decisions-2026-09.md`.

## After the review

Record what the lawyer said in `docs/decisions-2026-09.md`, change the terms and privacy pages, and change this file's status line.
