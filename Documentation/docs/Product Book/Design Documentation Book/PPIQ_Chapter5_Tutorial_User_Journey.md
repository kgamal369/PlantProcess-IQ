# PlantProcess IQ - Master Design Document

**Version 4.10 | Author: Karim, SOU Industrial Software, Dusseldorf**

> **Change log — Two-Release Production Roadmap and Day-1 Workbench Constitution (23 August 2026, v4.10).** v4.10 replaces retired internal programme codes with exactly two product releases: **M2 — Release 1, 30 September 2026**, for genuine early production and first-week customer work; and **M3 — Release 2, 30 October 2026**, for heavy production, higher data volume, more users and advanced intelligence. Each release uses only **P1, P2, P3, P4 and P5**. Release 1 makes DB Link/data onboarding, Canvas/data preparation, Jobs, enterprise BI reliability, read-only production OPC UA, governed References/Reconciliation/Assistant and minimum production hardening first-class release gates. Release 2 owns scale, advanced BI/authoring, deep enterprise administration, InsightBoard composition, multi-objective optimisation, customer-grade ROI convergence and heavy-production certification. Design and backlog are required to be one-to-one traceable: every designed product outcome has an execution owner and acceptance path, and every backlog task maps to an owning design contract.

> **Change log — Operational-Regime, Multi-Objective Practice and Period-Driver Hardening (22 August 2026, v4.9).** v4.9 closes the two generic gaps exposed by the first oil-plant requirement review without introducing oil-specific vocabulary: process transitions/changeovers and stabilisation become first-class governed context so statistics cannot mix distinct operating regimes; practice learning gains customer-declared multi-objective objective sets with Pareto/non-dominance and explicit preference resolution rather than silently choosing one KPI; exact period-to-period operational driver decomposition is added so the Assistant can explain changes in cost/productivity drivers from Layer-A facts before the monetary Value Engine is available. The release also binds the September checkpoint/fallback to the single v2.13 execution workbook. The six chapters remain the only design authority.


> **Change log - Chapter 5 Consistency Pass (v4.4 to v4.5).** No tutorial was redesigned and no capability was added. Journey mapping corrected so every tutorial states every J step it walks and J1 to J3 are declared commissioning prerequisites in 6.0.4a; T3 and T7 role prerequisites corrected to match Chapter 3 (Data Engineer authors, Administrator publishes; New definition on F4 is Administrator-only); the F4 **Target definition** selector and version policy are used, following the Chapter 3 4.5.5a gap this chapter exposed; T4 opens Genealogy Explorer at its `/materials` search landing state; the assistant is stated as Pro Plus and above and T5 step 25 is optional; **T8's remediation visibility corrected - a suppressed candidate is not shown at all**, and Reject and Defer are gated exactly as Accept; "analysis file" replaced throughout by **Analysis Definition**; the SQL contradiction, the once-per-source overstatement, the automatic-method overclaim, the q-value definition and the "every number" evidence claim are all corrected.

---

> **CURRENT AUTHORITY — Master Design v4.10.** PlantProcess IQ has exactly six current design-authority chapters and one current execution-authority backlog workbook. No other file may define, amend, override, supplement or reinterpret current product design or implementation scope. A design change edits the owning chapter directly; a scope change edits the backlog directly. Transitional reviews, amendment packs, ledgers, mandates and prior revisions are historical evidence only after their accepted content is integrated. Validation scripts are code/enforcement instruments, not design documentation.


# CHAPTER 5 - TUTORIAL: THE USER JOURNEY, STEP BY STEP

> **Target audience (6.5):** the person who will configure and use this software. They may have limited software knowledge, and they may have none at all. **No engineering or programming background is assumed anywhere in this chapter.**
>
> **Voice (6.6):** senior product owner. Patient, exact, and never assuming that the reader knows what a button does before it has been described.
>
> **Authority.** This chapter teaches the canonical journey defined in Chapter 2 3.3.1. It **does not define a second journey**. **The tutorials cover J4 to J15; J1 to J3 are commissioning prerequisites performed by an administrator before a user reaches this manual** (see 6.0.5). Each tutorial states **every** J step it walks through, not only its principal one. Every page code, route, button label, field name, message and error code used here is the one specified in Chapter 3 4.4 and Chapter 3 4.5.21. **If this chapter and Chapter 3 ever disagree about a control, Chapter 3 is correct and this chapter is a defect.**

---

## 6.0 How to read this chapter


### 6.0.0 Release-1 first-week production journey

M2 Release 1 is accepted only when a newly commissioned customer can complete the core workday through the product UI without a developer modifying product code: **create/test a DB Link or governed source → inspect and preview → configure mapping → prepare/transform in Canvas → validate and dry-run → save/version → create or bind a Job → schedule/run → monitor logs/history → open the resulting BI page → filter/drill → ask the governed Assistant for evidence-backed explanation**. Real OPC UA sources use the same downstream journey once their read-only edge connection is registered.

### 6.0.1 What you will be able to do at the end

Eight tutorials. Each one is a complete task with a beginning and an end, and each is broken into **at least fifteen numbered steps**. By the end of the eighth you will have taken data from your own plant databases and turned it into a page of charts, an analysis, and a scheduled result - **without writing a single line of code.**

| Tutorial | What you will achieve | Guideline | J steps actually walked |
|---|---|---|---|
| **T1** | Create a link to one of your plant databases and prove it works | 6.4.1 | **J4** |
| **T2** | Choose which tables and columns to bring in, and run the import | 6.4.2 | **J5, J6** |
| **T3** | Prepare and link the imported data so it fits the plant model | 6.4.3 | **J7** |
| **T4** | Schedule the load into the plant model, clear quarantine, verify genealogy | 6.4.4 | **J8, J9** |
| **T5** | Build an analysis page and put your first chart on it | 6.4.5 | **J10, J11** |
| **T6** | Create an Analysis Definition: statistics, correlation or machine learning | 6.4.6 | **J12** |
| **T7** | Attach it to a job so it runs on a schedule, and watch it | 6.4.7 | **J12, J15** |
| **T8** | Show what the analysis produced, and act on a prediction | 6.4.8 | **J10, J11, J13, J14** |

**Coverage.** T1 to T8 walk **J4 to J15** in full. **J1 to J3 - install, licence activation and the creation of users and roles - are commissioning prerequisites** carried out by an administrator before you receive your account, and they are specified in Chapter 3 DF1's preconditions rather than taught here. Section 6.0.5 tells you what must already be true.

**Do them in order.** Each tutorial uses what the one before it created.

### 6.0.2 How a step is written

Every step tells you three things: **where to click, what happens, and how you know it worked.**

> **Step 4.** In the **main menu** on the inline-start edge of the screen, open the **Data Integration** group and select **Connections**.
> *The Connections page opens. You will see two panels stacked one above the other.*
> **You know it worked when:** the page title reads "Data Integration" and the first panel is headed "DB Link Configuration".

Where a step asks you to type something, the exact text appears in a box. Where a step warns you about something that commonly goes wrong, it is marked **Careful**.

### 6.0.3 Two words about sides

This manual never says "left" or "right". It says **inline-start** and **inline-end**.

The reason is practical: in English and German the inline-start edge is the left one, and in Arabic it is the right one. The software mirrors itself automatically. **Inline-start means the edge where a line of text begins in your language, and inline-end means the edge where it finishes.** Block-start means the top of an area; block-end means the bottom.

### 6.0.4a Commissioning prerequisites - J1, J2 and J3

These three journey steps happen **before** this manual applies, and an administrator performs them. You do not do them and you cannot do them without the administrator role.

| Step | What happened | Where | How you can tell it was done |
|---|---|---|---|
| **J1** | The product was installed and first opened. **The plant schema contained zero rows** | F7 System Settings | You have an address that loads the Login page |
| **J2** | The signed licence was applied, and the tier and capacity envelope became visible | F2 Licence and Entitlement | Your header shows a tier badge |
| **J3** | Users and roles were created, and authoring quota assigned per role | F1 Users and Roles, F3 Quota | You have a username, and pages appear in your menu |

**If something in a tutorial is missing or greyed out, one of these three is usually the reason** - most often J3, because a role or a quota was not assigned. The tutorial will tell you which, and the answer is a conversation with your administrator rather than a workaround.

### 6.0.4 What you need before you start

| You need | Why | Who gives it to you |
|---|---|---|
| A user account and password | To sign in | Your administrator, created at commissioning |
| Your role | Some steps need the Data Engineer role | Your administrator, on page F1 |
| The address of one plant database | T1 will connect to it | Your IT department |
| A **read-only** username and password for that database | The product will refuse an account that can write | Your database administrator |
| Permission to read from it inside a stated time window | So imports do not disturb production | Your database administrator |

**Careful.** Ask your database administrator for a **read-only** account. If the account can change data, the connection test in T1 will fail on purpose, with the message that read-only verification failed. That is the product protecting your plant, not a fault.

---



### 6.0.4b v4.7 generic customer-data additions to the existing eight tutorials

The canonical journey remains J1-J15 and the tutorial count remains eight. The following requirements are integrated into the existing steps rather than creating an oil-specific tutorial:

- **T1/T2:** where a source carries historian/OT data, register its time authority, timestamp basis, quality semantics, sampling/deadband information and read-only capability truth. A connector cannot advertise browse/read/subscribe when the build does not execute it.
- **T3/T4:** declare Analysis Grain and subject identity through definitions/registry. A material unit is optional; equipment/process-window and continuous-flow interval are valid subjects.
- **T5:** a widget may bind Performance Reference measures such as gap and in-envelope state. New customer dimensions appear from registry rows without a code change.
- **T6:** selecting a parameter/measure shows its aggregation semantics. If none is declared, Run is blocked with `AG01` rather than silently averaging.
- **T8:** when overlapping independent history exists, Findings may include an Operational Evidence Reconciliation case with evidence handles and causal-confidence level. The tutorial never uses the phrase "lie detection" and never attributes intent to a person.
- **Assistant:** the dock may explain a reconciliation/reference result only from governed evidence. Investigation-board composition is an Advanced capability and uses the same Page Builder/query contracts.

## 6.1 The screen, once, so the tutorials can be short

Every page in the product is built the same way. Learn these six things once and every tutorial becomes easier.

```
+----------------------------------------------------------------------+
|  SITE NAME        breadcrumb          [tier]  [activity]  [search] [you] |   <- G2 header
+--------+-------------------------------------------------------------+
|        |                                                             |
|  MAIN  |   THE PAGE                                                  |
|  MENU  |                                                             |
|        |                                                             |
|  A..F  |                                                             |
|        |                                                    (o) <----+---- G1 Assistant
+--------+-------------------------------------------------------------+
```

| # | Thing | Where | What it does |
|---|---|---|---|
| 1 | **Main menu** | Inline-start edge, full height | Every page you are allowed to open, grouped. Groups you have no permission for are **not shown at all** - if you cannot see a page, it is not broken, it is not yours |
| 2 | **Header** | Block-start, full width | Your site name, where you are (the breadcrumb), your licence tier, the activity indicator, search, and your own menu |
| 3 | **Assistant** | **Pro Plus tier and above.** When your tier includes it, a round button at the inline-end, block-end corner of **every authenticated page**. Below Pro Plus it is **absent**, not present and broken | Ask a question in plain language. It answers about the page you are on, and every figure it gives you carries a chip you can click to see its evidence |
| 4 | **Activity tray** | Header, next to search | Anything long-running that you started. **You can leave a page and it keeps going**; the tray tells you when it finished |
| 5 | **Search** | Header | Finds a page, a field, a measure, a saved definition or a result. Opens with a keyboard shortcut |
| 6 | **Messages** | A small card wherever the problem happened | Green confirms, amber warns, red is a problem. **A red card always tells you what went wrong and what would fix it** |

### 6.1.1 The three kinds of message, and how to tell them apart

This matters more than it sounds, because reacting correctly saves hours.

| Colour | Means | What you do |
|---|---|---|
| **Green** | It worked | Nothing. It disappears by itself |
| **Amber** | It worked, but you should know something | Read it. Usually it is warning you that a choice will have a consequence later |
| **Red, with a sentence and a code** | The product refused, on purpose | **Read the sentence.** It says what it refused, why, and what would satisfy it. This is not a crash |
| **Red, saying the request failed** | Something technical went wrong | Press **Retry**. If it happens twice, note the code and tell your administrator |

**The product refuses on purpose quite often, and that is deliberate.** It refuses to guess. When it says "this analysis needs 60 units and you have 42", it is not broken - it is telling you it will not give you an answer it cannot defend.

### 6.1.2 If a button is grey

A grey button is not broken. **Hover over it and it will tell you what is missing.** Usually you have not filled a field it needs, or a step earlier in the journey has not been completed.

---

## 6.2 TUTORIAL T1 - Connect to one of your plant databases

**Guideline 6.4.1. Journey step J4. You will need:** the Administrator or Data Engineer role, and the read-only database details from 6.0.4.

**What you are about to do.** You are going to tell the product where one of your databases lives and how to read it. Nothing is imported yet. At the end of this tutorial the product knows the address, has proved it can read, and has proved it **cannot** write.

---

**Step 1.** Open your browser and go to the address your administrator gave you. The **Login** page opens (`/login`). You will see a single card in the middle of a dark screen, with the product name above it.

**Step 2.** Click in the **Username** field and type your username.

**Step 3.** Press the **Tab** key. The cursor moves to the **Password** field. Type your password.

**Step 4.** Click **Sign in**, the wide blue button at the bottom of the card. You may also just press **Enter**.
*If you are an administrator you will be asked for a second code. Enter it and continue.*
**You know it worked when:** the dark screen is replaced by the **Home** page, and the main menu appears along the inline-start edge.

> **Careful.** If sign-in fails, the message says only that the credentials failed. It deliberately does not say whether the username or the password was wrong, because telling you would also tell an intruder.

**Step 5.** Look at the **Home** page for a moment. Across the block-start you will see the **journey rail**: a row of ten small markers showing how far commissioning has progressed. The one you are working on is highlighted in bright cyan.

**Step 6.** In the **main menu** on the inline-start edge, click **Data Integration**. The group opens and shows six entries: Connections, Table Registry, Prepare Import, Importing, Jobs Monitor, Connector Truth.

**Step 7.** Click **Connections**.
*The Connections page opens (`/data-integration/connections`).*
**You know it worked when:** the page header reads **Data Integration**, and underneath it the line **"Connections are read-only toward your source systems at all times."** That line never goes away. It is a promise.

**Step 8.** Look at the two panels. The block-start panel is **DB Link Configuration** and lists connections you already have - probably none yet. The block-end panel is **Supported Connectors** and shows every kind of database the product can talk to.

**Step 9.** In the **Supported Connectors** panel, find the kind of database you are connecting to. Some cards are bright and clickable; some are dimmed with a small **Planned** badge.
*A dimmed card means that connector is not available yet. The product tells you honestly rather than letting you try and fail.*

**Step 10.** In the **DB Link Configuration** panel header, at the inline-end, click **New Connection Profile** (the blue button with a plus sign).
*The panel changes: the list disappears and a form takes its place. A **Back** button appears at the inline-start of the same header.*

**Step 11.** In the **Name** field, type a name you will recognise later. For example:

```
Line 1 Tracking Database
```

*The name is only for you. Choose something a colleague would also understand.*

**Step 12.** Leave the **Code** field empty. The product will generate one.

**Step 13.** Open the **Provider type** dropdown and choose the kind of database you are connecting to.
**Watch what happens:** some fields on the form appear and others disappear. This is correct. Different databases need different questions, so the form changes to ask only what yours needs.

**Step 14.** Fill in the connection fields your IT department gave you, moving through them with the **Tab** key:

| Field | What to type |
|---|---|
| **Host** | The server address or name |
| **Port** | The port number. It is usually already filled with the normal one for your database type |
| **Database** | The database name |
| **Schema** | The schema name, if your database uses one |
| **Username** | The **read-only** username |
| **Password** | Its password. The characters are hidden as you type |

**Step 15.** Open the **Source system tag** dropdown and choose what kind of system this is: MES, Level 2, Historian, LIMS, ERP or Inspection.
*This is a label, not a behaviour. It records where the data came from so that later, when you are looking at a chart, you can tell which system a number came from. **It does not change how the data is read.***

**Step 16.** Scroll to the group of fields headed with the load budget. These four fields protect your plant. Fill them with what your database administrator agreed:

| Field | What it means in plain language |
|---|---|
| **Max rows** | The most rows the product may read in one go |
| **Timeout** | How many seconds a single read may take before it is cancelled |
| **Requests per minute** | How often the product may ask your database for anything |
| **Approved window** | The hours and days when reading is permitted at all |

> **Careful.** These are not suggestions. **The product checks them before it touches your database**, not after. If a read would break one of them, it is refused and your database never sees the request.

**Step 17.** Click **Test connection**, the secondary button at the bottom of the form.
*Wait a moment. A result appears in the form.*

**Step 18.** Read the result:

| What you see | What it means | What to do |
|---|---|---|
| Green, with a response time | Reachable, credentials work, **and the account is confirmed read-only** | Continue to step 19 |
| Red, `CN01` host unreachable | The address or port is wrong, or a firewall is in the way | Check the address with IT |
| Red, `CN02` authentication failed | Username or password is wrong | Check with your DBA |
| Red, `CN04` cannot read the schema | It connected, but the account has no permission | Ask your DBA to grant read access |
| Red, `CN03` **read-only verification failed** | **The account can change data.** The product refuses it | Ask your DBA for a read-only account. Do not work around this |

**Step 19.** When the test is green, click **Save**, the blue button at the inline-end of the form footer.
*A green message confirms it. The form closes and the list returns, now with your connection in it.*

**Step 20.** Look at your new row. At its inline-end are four small icons: **Edit**, **Test**, **Activate** and **Deactivate**. Click **Activate**.
**You know the whole tutorial worked when:** your connection appears in the list, its state shows active, and re-opening it with **Edit** shows the password as dots. **The product never shows you a stored password again, not even to an administrator.**

---

**What you achieved in T1.** The product now knows one of your databases and has proved twice over that it can only read from it. Nothing has been imported. **Your database has not been changed in any way, and it never will be.**

---

## 6.3 TUTORIAL T2 - Choose your data and bring it in

**Guideline 6.4.2. Journey steps J5 and J6. You will need:** the connection from T1, and the Data Engineer role.

**What you are about to do.** You will browse your own database from inside the product, choose which tables to bring in, choose which columns of those tables matter, tell the product how to bring in only what is new, and then watch the first import run.

---

**Step 1.** In the **main menu**, under **Data Integration**, click **Table Registry**.
*The Table Registry page opens (`/data-integration/registry`).*

**Step 2.** At the block-start of the inline-start panel, open the **Connection selector** dropdown and choose the connection you made in T1.
*After a moment a tree appears below it.*
**You know it worked when:** you can see **your own schema and table names** in the tree. This is your database, live. Nothing has been copied yet.

**Step 3.** Click the small triangle beside a schema name to open it. Click the triangle beside a table name to open that.
*Three levels: schema, then table, then columns. Each column shows its type beside its name.*

**Step 4.** If the list is long, use the **Filter tables** search box above the tree and type part of a name.

**Step 5.** **Decide which table to register first, and this decision matters.** If you have a table that contains **definitions** - a list of defect names, a list of parameter names, a list of codes and what they mean - **register that one first.**

> **Why.** Those tables are your plant's vocabulary. When the product later imports a row that says defect code `X`, it needs the table that says what `X` means. If the vocabulary is not in yet, those rows will be set aside and you will have to run the load again.

**Step 6.** Click on the table you have chosen. It highlights.

**Step 7.** At the inline-end of that tree row, click **Register**.
*A green message confirms it, and the table appears in the registered list on the inline-end of the page.*

**Step 8.** Repeat steps 6 and 7 for a second table - this time a table with real production data, such as measurements or inspection results.

**Step 9.** In the **main menu**, click **Prepare Import**.
*The Prepare Import page opens (`/data-integration/prepare`). Your registered datasets are listed on the inline-start.*

**Step 10.** Click your first dataset in that list. Three groups appear: **Columns**, **Business key** and **Watermark**.

**Step 11.** In the **Columns** group, every column is ticked by default. Untick any you do not need.
*Fewer columns means faster imports and less storage. You can always add one later.*

**Step 12.** In the **Business key** group, choose the column - or columns - that **uniquely identify one row** in this table. This is usually the identifier your plant already uses for a piece, a batch or a coil.
*If it takes more than one column, choose them in order. **Order matters here**, so pick them the way your plant would say them aloud.*

**Step 13.** In the **Watermark** group, open the dropdown and choose the column the product should use to know what is new. This is almost always a **timestamp** - the moment the row was created or last changed.
*Only columns that can be put in order appear in this list. A column that cannot be ordered cannot tell the product what is new.*

> **Careful.** If you leave the watermark empty you will see an **amber warning**: *"Without a watermark every run re-reads the whole table."* That is not an error and you may continue - but every import will read your entire table every time. On a large table that is slow for you and heavy for your database. **The product will also force this dataset to import at most once a day**, and tell you why.

**Step 14.** Click **Save preparation**, the blue button at the inline-end of the footer.
*A green message confirms it.*

**Step 15.** Repeat steps 10 to 14 for your second dataset.

**Step 16.** In the **main menu**, click **Importing**.
*The Importing page opens (`/data-integration/importing`). It is empty because nothing has run yet.*

**Step 17.** In the batch panel header, click **Run due imports**, the blue button.
*The button becomes busy. Rows begin to appear in the table below.*

**Step 18.** Watch the rows. You will see the source object name, a status, a start time and a row count that climbs as it reads.
*You do not have to stay here. Look at the **activity indicator** in the header - it shows the run is still going. **You can go to another page and come back**, and the import keeps running.*

**Step 19.** Wait for the status to reach its final state.

| Status | Meaning |
|---|---|
| **Completed** | It finished. The row count tells you how many rows arrived |
| **Completed, marked partial** | It hit your Max rows limit and stopped politely. **The next run continues where this one stopped** - nothing is lost |
| **Failed** | It stopped, and the row tells you why. Expand the row to read it |

**Step 20.** Click **Run due imports** a second time.
**You know the whole tutorial worked when:** the second run finishes quickly and brings in **few rows or none at all**.
*That is the watermark doing its job. The product asked your database only for what changed since last time. This is the single most important thing to verify, because it is what makes the product safe to run every few minutes on a production database.*

**Step 21.** In the **main menu**, click **Jobs Monitor** (`/data-integration/jobs`). Find your import job in the table.
*The columns are Job, Type, Target, Status, Last Run, Duration, Runtime and Actions. At the inline-end of the row are **Run now**, **Pause** and **Resume**.*

**Step 22.** Click **Pause** on your import job, then click **Resume**.
*The status pill changes and changes back. **Pausing survives a restart** - if the product is restarted tonight, a paused job stays paused.*

---

**What you achieved in T2.** Your own data is now inside the product, in a holding area called **staging**, exactly as it was in your database - nothing has been interpreted or changed. **No chart can read it yet.** That is deliberate, and T3 is where you fix it.

---

## 6.4 TUTORIAL T3 - Prepare and link your data so it fits the plant model

**Guideline 6.4.3. Journey step J7.**

**You will need:** the two datasets from T2, and **two roles between you**:

| Role | Which steps |
|---|---|
| **Data Engineer** | Steps 1 to 20 - building, previewing and saving the draft |
| **Administrator** | **Step 21, Publish version** |

> **Why publishing is a separate role.** Publishing freezes an immutable version **and emits the relationship model that sixteen other parts of the product will rely on** (Chapter 3 4.5.10). It is a governed act, not a save. If you are a Data Engineer, build the definition and validate it, then ask your administrator to publish it - the **Publish version** button will be visible but disabled for you, and hovering over it says so.

**What you are about to do.** This is the most important tutorial in the manual, and the one that makes everything afterwards possible.

Your data is currently sitting in staging, shaped exactly like your source system. The product cannot analyse it yet, because it does not know what any of it means. **You are going to tell it** - by dragging tables onto a board, drawing lines between them, and saying which column of yours is which part of the plant model.

**What you build here becomes your plant's permanent model of itself**, and every chart, every analysis and every prediction afterwards uses it.

**You create the initial mapping once, then version it** when the source schema changes, when the plant model grows, or when a later tutorial needs a relationship you have not yet declared. T8 and the troubleshooting table both send you back here for exactly that reason. **Versioning is normal and expected; the published version you replace is never lost.**

**You will not write any code.**

---

**Step 1.** In the **main menu**, under **Data Preparation**, click **Transformation Studio**.
*The Transformation Studio opens (`/prep/canvas`). It looks busy. It is not - it has four areas and you only need three of them today.*

**Step 2.** Find the four areas and name them to yourself:

| Area | Where | What is in it |
|---|---|---|
| **The schema tree** | Inline-start edge | Your tables. **On this page only, it has two groups**: your staged tables at the block-start, and the plant model at the block-end |
| **The board** | The middle | Empty, for now. This is where you will work |
| **The toolbox** | Inline-end edge | Blocks you can drag onto the board, grouped and searchable |
| **The debug log** | Block-end, along the bottom | Empty. It will talk to you as you work. **Read it whenever it changes** |

**Step 3.** At the block-start of the page find the **Mode toggle**, a small two-part control reading **Block** and **SQL**. Make sure **Block** is selected.
*Block mode is drag-and-drop. SQL mode is for someone who writes database queries. **They produce exactly the same thing**, and you never need SQL for normal work.*

**Step 4.** In the schema tree, open the **staged tables** group and find the vocabulary table you registered first in T2.

**Step 5.** **Drag that table from the tree onto the middle of the board.**
*It becomes a box. Down its side are its columns, each with a small coloured dot beside it.*

**Step 6.** Look at the coloured dots. **The colour tells you the type of that column:**

| Colour | Type |
|---|---|
| Bright cyan | A key - an identifier |
| Blue | A number |
| Pale blue-grey | Text |
| Purple | A date or time |

*These are not decoration. **You will not be allowed to connect two dots of incompatible types**, and that is what stops most mistakes before they happen.*

**Step 7.** Drag your second staged table onto the board, below the first.

**Step 8.** Now the important part. Find the column in the first box that identifies a piece of material, and the column in the second box that identifies **the same physical thing**. They will almost certainly have **different names**, because they come from different systems. That is precisely the problem this product exists to solve.

**Step 9.** **Click the dot beside the first column and drag a line to the dot beside the second column.**
*A line is drawn between them, and it is labelled with the equality it represents.*

**You know it worked when:** the line stays, and the debug log at the block-end shows a green success line.

> **If the line refuses to land**, read the debug log. It will tell you exactly why in a sentence - for example that one dot carries rows and the other expects a single value. **The line not landing is the product protecting you.** A wrong connection here would silently produce wrong answers in every chart for years.

**Step 10.** Click on the connecting line to select it. In the panel that appears at the inline-end, check the join settings:

| Setting | What to choose |
|---|---|
| **Join type** | Usually **inner**, which keeps only rows that exist in both |
| **Key pairs** | The columns you connected. Add more if it takes more than one column to match a row |
| **Grain on each side** | What one row means on each side - a piece, a batch, a coil |

**Step 11.** If the two sides have **different grains** - for example one row per batch on one side and one row per piece on the other - a further field appears asking for the **attribution rule**.
*This is asking: when one parent produces several children, how should the parent's value be shared among them? Choose the rule that matches your process. The weights must add up to exactly one for each child, and the product will check that for you.*

**Step 12.** In the **toolbox** at the inline-end, find the **Output to canonical entity** block and drag it onto the board.

**Step 13.** Draw a line from the output of your joined data into this new block.

**Step 14.** Click the **Output** block. In the panel at the inline-end, open **Target entity** and choose what this data actually is - for example a material unit, a parameter observation, a quality event.
*Once you choose, the panel fills with the fields that entity needs.*

**Step 15.** Map each field: for every field on the inline-start, choose which of **your** columns fills it, using the dropdowns. Where a field should always have the same fixed value, type it with `const:` in front, like this:

```
const:LINE1
```

**Step 16.** Watch the small badge on each block as you work:

| Badge | Meaning |
|---|---|
| Green | This block is fine |
| Amber | It will work, but read the debug log |
| Red | It cannot run. The log says exactly which rule is broken |

**Step 17.** Look at the **Validity indicator** at the block-start, beside the Run button. It reads either **Valid flow** in green, or **Invalid** in red.
*If it is red, you cannot run yet, and that is deliberate - the product will not start something it already knows will fail. Read the log and fix what it names.*

**Step 18.** Click **Preview (dry-run)**.
*Sample rows appear for each block, with a count. **Nothing has been saved and nothing has been written to the plant model.** This is a safe look.*

**Step 19.** Read the preview carefully. Do the values look like what you expected? Does the row count look sensible?
*If the joined row count is **much larger** than either input, the debug log will warn you. That usually means the key pair is not unique and you should add a second key column in step 10.*

**Step 20.** Optional but recommended: click **Compiled SQL**.
*A read-only window shows the actual database statement your diagram produces. **You do not need to understand it.** It is there so that your database administrator can look at it and be satisfied, which is often what unblocks an installation.*

**Step 21.** **This step requires the Administrator role.** Click **Publish version**, the blue button.
*If you are a Data Engineer the button is disabled; hover over it and it names the role required. Your draft is saved and validated, so an administrator can publish it without redoing your work.*
*Two things happen. First, this version is frozen and can never be edited again - editing later creates a new version, and the old one remains for reference. Second, and more importantly, **the links you drew are published as your plant's relationship model.***

**Step 22.** In the **main menu**, under **Data Preparation**, click **Relationship Browser** (`/relationships`).
**You know the whole tutorial worked when:** you can see the link you drew, listed as a relationship, with its entities, its key columns in order, and its grain on both sides.

**Step 23.** Click the relationship to open its detail. Check three things:

| Field | What you want to see |
|---|---|
| **Validation state** | If it says **unproven**, click **Validate against data** and wait |
| **Ambiguity** | It should say unambiguous. If it says ambiguous, two routes exist between the same two things and you must choose which is preferred |
| **Members** | Your key columns, in the order you chose |

> **Why this page matters more than it looks.** Sixteen different parts of the product read this one relationship: the charts, the filters, the genealogy, the statistics, the machine learning, the predictions and the assistant. **You declared it once, here. Nothing downstream will ever guess it again.**

---

**What you achieved in T3.** You built your plant's model of itself. This is the thing an external consultant would have taken with them when they left. **It is now yours, it is versioned, and you can export it.**

---

## 6.5 TUTORIAL T4 - Schedule the job that loads prepared data into the plant model

**Guideline 6.4.4. Journey steps J8 and J9.** *J8 is the projection and its quarantine; J9 is verifying the genealogy in steps 16 to 20.*

**What you are about to do.** In T3 you defined *how* staged data becomes plant data. Now you will run it, check what it produced, deal with anything it could not accept, and put it on a schedule so it keeps happening without you.

---

**Step 1.** In the **main menu**, click **Importing** (`/data-integration/importing`).

**Step 2.** Scroll to the block-end of the page, to the schedule card.

**Step 3.** Open the **Mapping selector** dropdown and choose the definition you published in T3.

**Step 4.** In **Interval minutes**, replace the default with how often you want the plant model refreshed. Fifteen is a sensible starting point.

**Step 5.** Click **Save schedule**.
*A green message confirms that the schedule was saved and the job created.*

**Step 6.** In the **main menu**, click **Jobs Monitor**. Find the new job - its type will show as a canonical refresh.

**Step 7.** At the inline-end of its row, click **Run now**. Do not wait for the schedule the first time.

**Step 8.** Watch the status. When it finishes, click the row to expand it.

**Step 9.** Read the three numbers the run reports. **These three numbers are the whole point of this step:**

| Number | Meaning |
|---|---|
| **Mapped** | Rows that became plant data. This is the good number |
| **Quarantined** | Rows the product refused, **individually**, and set aside with a reason |
| **Total** | The two added together |

> **Quarantined is not failure.** The product refuses a bad row rather than putting a wrong value into your plant model, because a wrong value there would poison every chart and every analysis afterwards. **The batch continues; only the bad rows stop.**

**Step 10.** If Quarantined is zero, skip to step 16. If it is not, continue - and it very often is not on a first run.

**Step 11.** In the **main menu**, under **Data Preparation**, click **Mapping Health** (`/mapping-health`).

**Step 12.** Find the quarantine section. Rows are **grouped by reason**, not listed one by one, with a count for each group and a few examples.

**Step 13.** Read the group headings. Each is a plain sentence, for example:

| Code | The sentence you will see |
|---|---|
| `PV02` | "Row 219: `temp` = `n/a` cannot become a number." |
| `PV03` | "Row 77: `observed_at` is empty; ParameterObservation requires a time." |
| `PV06` | "Row 91: defect code `<code>` is not in the imported catalogue." |
| `PV04` | "Rows 12 and 3,405 both declare material `<code>` for site `<site>`." |

**Step 14.** Each group also carries a **suggested correction**. Read it.
*A `PV06` almost always means the vocabulary table has not been imported yet, or was imported after the data. Go back to T2, register it, import it, and these rows will resolve themselves.*

**Step 15.** Fix the cause. Then, back on Mapping Health, click **Reprocess quarantined**.

> **This is the part worth remembering.** Reprocessing only reruns the rows that were set aside. **It does not re-import from your database and it does not reprocess the rows that were already fine.** On a large table this is the difference between a minute and an afternoon.

**Step 16.** In the **main menu**, under **Data Preparation**, click **Genealogy Explorer**.
*The page opens at `/materials`, its **search landing state**: a search box, your recently viewed units, and an empty detail region. This is J9 beginning.*

**Step 17.** In the search box, type an identifier you know exists in your plant - a piece number, a batch number, a unit number. Press **Enter**, then select the result.
*The page moves to `/materials/{id}`, the unit state.*

**Step 18.** The unit opens. Look at what is on the screen:

| Region | What it shows |
|---|---|
| Block-start strip | Where this came from: the source system, the source record, the import batch |
| The graph | This unit's parents and children |
| The thread | Everything that happened around it in time - measurements, events, quality results |

**Step 19.** Use the **direction toggle** to switch between backward, forward and both. Use the **depth** stepper to go further.
*If a unit has two parents, you will see a small weight on each line. **Those weights always add up to exactly one.** That is how the product shares a parent's measurement fairly between its children.*

**Step 20.** Click **Drill to source rows** on any node.
**You know the whole tutorial worked when:** you land on the actual rows from your own database that produced this.

> **Every plant-data and intelligence figure resolves to its evidence and provenance**, and where a figure is derived from plant data that chain reaches the contributing source rows. *Some figures in the product are not derived from plant data at all - a quota, a licence value, a model metric, a measured latency, a log count, a cost assumption you entered - and those resolve to their own configuration or measurement record rather than to a source row.*

**Step 21.** Go back to **Jobs Monitor** and confirm your canonical refresh job now has a schedule and a next run time.

---

**What you achieved in T4.** Your plant model is populated, it refreshes itself, bad rows are quarantined rather than accepted, and you can trace any unit back to the source row it came from.

---

## 6.6 TUTORIAL T5 - Build an analysis page and put a chart on it

**Guideline 6.4.5. Journey steps J10 (build the page) and J11 (explore it associatively). You will need:** plant data from T4, and the Engineer role or higher.

**What you are about to do.** Build a page from nothing, put a chart on it, connect that chart to your data, add a filter, and then see the thing that makes this product different from a spreadsheet: **click one value anywhere and every chart on the page responds.**

---

**Step 1.** In the **main menu**, under **Analysis**, click **Page Builder** (`/page-builder`).

**Step 2.** In the inline-start header, click **Create page**.
*A short form appears.*

> **If Create page is grey**, hover over it. You have probably reached the number of pages your role is allowed. Your administrator can raise it on the quota page. **The limit exists so that one enthusiastic person cannot fill the installation with expensive pages**, not to obstruct you.

**Step 3.** Give the page a name a colleague would understand, for example:

```
Line 1 Daily Quality
```

**Step 4.** Choose which roles may see this page, then confirm.
*An empty grid appears with the message that the page has no widgets yet, and an **Add widget** button in the middle of it.*

**Step 5.** Click **Add widget**.
*A picker opens showing the kinds of widget you can add: chart, table, KPI, calculated label, calendar filter, filter.*

**Step 6.** Choose **chart**. Click **Next**.

**Step 7.** Give the widget a name and click **Next**.
*The authoring panel opens. **This is the same shell you used in T3**, opened for a different purpose. The layout will feel familiar, which is the point.*

**Step 8.** At the block-start of the panel find the **Binding mode** toggle: **Catalogue** and **Query**. Leave it on **Catalogue**.
*Catalogue is the simple way: choose from lists. Query is for someone who wants to write their own. **You can switch to Query later and your catalogue choices come with you** - nothing is lost by starting simple.*

**Step 9.** Open **Chart type** and choose **Bar**.

**Step 10.** Open **Dimension** and choose what to group by - for example a defect type or an equipment code.
*Everything in this list came from **your own data**. The product ships with no plant vocabulary at all. If something you expect is missing, it means that column has not been mapped yet in T3.*

**Step 11.** Open **Measure** and choose what to count or average.

> **Watch this.** If you change the chart type to one that does not use a dimension, **the Dimension field disappears** rather than staying and being ignored. The form only ever shows you fields the chart you chose actually uses.

**Step 12.** Click **Preview**.
*The chart renders at the size it will really be, with your real data in it.*

**Step 13.** If it looks right, click **Save**.
*The panel closes and your chart appears on the page grid.*

**Step 14.** Add a second widget: click **Add widget** again, choose **KPI**, name it, choose a measure, preview, save.

**Step 15.** Now add a **filter**. Click **Add widget**, and this time choose **filter** from the picker.
*A filter is a widget like any other. **It is not a fixed part of the page.** Every plant filters by different things, so you build the filters you need.*

**Step 16.** Choose the field to filter on and the kind of filter - a list, a dropdown, a date range, a number range. Save it.

**Step 17.** Drag your widgets to arrange them. Grab a widget by its header and move it; the others move out of the way as you go. Drag any edge or corner to resize.

**Step 18.** Click **Save layout** in the page header.

**Step 19.** Reload the page in your browser.
**You know it worked when:** everything is exactly where you left it.

**Step 20.** In the **main menu**, under **Analysis**, click **Interactive Workspace** and open the page you just built.

**Step 21.** **Click on one bar in your bar chart.**
*Watch the whole page. Every widget re-queries. A chip appears in the selections bar at the block-start naming what you selected.*

**Step 22.** Look at the strip above the grid. Values are now in different states:

| Appearance | Meaning |
|---|---|
| **Green** | You selected this |
| **Normal white** | Still possible alongside your selection |
| **Grey and struck through** | Not possible alongside your selection |

**Step 23.** **Now click a grey struck-through value.**
*The selection does not ignore you and it does not clear. It **pivots** to what you just clicked.*

> **This is the behaviour worth learning.** Clicking an impossible value is you saying "no, I meant this one". Most tools ignore that click. This one understands it.

**Step 24.** Click **Clear all** at the inline-end of the selections bar. The bar returns to reading that no selections are applied.

**Step 25. Optional - Pro Plus tier and above.** If your tier includes the assistant, click its button at the inline-end, block-end corner and ask, in your own words, about what you are looking at.
*If there is no such button, your tier does not include the assistant. **Nothing in this tutorial depends on it**, and everything you built above works exactly the same.*

**You know the whole tutorial worked when:** your page renders, clicking a bar cross-filters every widget, clicking an excluded value pivots the selection, and the layout survives a reload. If you do have the assistant, its answer carries an evidence chip under each figure that you can click.

---

**What you achieved in T5.** You built a working analysis page without code, and you saw the associative behaviour that makes exploring data fast rather than tedious.

---

## 6.7 TUTORIAL T6 - Create an Analysis Definition: statistics, correlation or machine learning

**Guideline 6.4.6. Journey step J12. You will need:** plant data from T4. Machine-learning methods require the Pro Plus tier.

**What you are about to do.** So far every chart has shown you what happened. Now you will ask the product to **find something you did not know** - which conditions in your process are associated with which outcomes.

You will not do any mathematics. You say **what to relate to what**, and the product **selects an eligible method from the registered method rules and the data types involved, validates that method's assumptions against your data, applies it, and records which method it used and why.**

*It does not claim to have found "the correct method" - it records the method it chose and the basis for choosing it, so a statistician in your organisation can check that judgement.*

---

**Step 1.** In the **main menu**, under **Analysis**, click **Analysis Toolbox** (`/analysis/toolbox`).

**Step 2.** In the inline-start header, click **New definition**.
*Three blocks appear in the middle of the page: **Outcome**, **Grain** and **Window**. On the inline-end are two panels: the payload panel and the readiness panel.*

**Step 3.** Open the **Outcome** block. Choose what you want to understand - a defect class, a downtime cause, a yield measure.
*Everything in this list came from your data. If what you want is not there, it has not been mapped yet.*

**Step 4.** Open the **Grain** block. Choose the level you want to analyse at - per piece, per batch, per coil.

**Step 5.** Open the **Window** block. Choose how far back to look.
*Longer windows give more data and therefore more reliable answers, but they also mix in older operating conditions. Start with something that represents how you run today.*

**Step 6.** Open the **Method** block. **Leave it on automatic unless you have a reason not to.**
*Automatic means the product selects an eligible method based on the data types involved and the registered method rules, then checks that method's assumptions against your data. **The method it used is recorded with every result**, so you always know what was applied. If you force a method whose assumptions your data does not meet, it tells you rather than running it anyway.*

**Step 7.** Look at the **payload panel** on the inline-end. It shows exactly what will be sent to the engine.
*Below it is a line reading **IDENTICAL**. That is the product proving that what you see here is precisely what the engine will receive - not a summary of it.*

**Step 8.** Click **Check readiness**.
*The readiness panel fills with five rows. **This is the most important screen in the product and it is worth understanding properly.***

**Step 9.** Read the five rows. Each says what was measured, what is required, and whether it passes:

| Dimension | The question it asks |
|---|---|
| **Independent units** | Are there enough separate pieces to draw a conclusion from? |
| **Outcome events** | Did the thing you are studying happen often enough? |
| **Minority-class balance** | Is the rarer case rare enough to be meaningless? |
| **Freshness** | Is the data recent enough to describe how you run now? |
| **Completeness** | Are the required fields actually filled in? |

**Step 10.** Look at the overall verdict at the block-start of the panel:

| Verdict | What it means | What to do |
|---|---|---|
| **Ready** | Every dimension passes | Continue to step 12 |
| **Partial** | It will run, but at least one dimension is weak | You may continue; treat the result with the caution the panel names |
| **Blocked** | At least one dimension fails | It will not run. Read which one, and by how much |

> **The overall verdict is the worst of the five, never an average.** One failing dimension blocks the run even if the other four are excellent. That is deliberate: a conclusion is only as good as its weakest support.

**Step 11.** If it is **Blocked**, read the failing row. It gives you a measured number and a required number, for example that 60 independent units are needed and 42 are present.

> **This is not a fault, and there is no way to switch it off.** Your options are: widen the window so more units are included, wait until more data arrives, or choose a different outcome that has occurred more often. **Nobody in the product - not you, not your administrator, not any automated process - can lower the threshold to force a result.** That is the whole reason you can trust the results you do get.

**Step 12.** When readiness allows it, click **Save definition**.
*Your analysis is now an **Analysis Definition**: named, versioned, and stored in the product's definition store. **It is not a file on your computer.** You can open it, copy it, version it and export it - and the export is a portable artifact for transfer and audit, never the source of truth.*

**Step 13.** Click **Run governed analysis**.
*A run begins. The activity indicator in the header shows it. **You can leave this page.***

**Step 14.** Wait for the run to finish, then go to **Findings** in the main menu (`/correlations`).

**Step 15.** Look at the table. The columns are Feature, Outcome, Method, Effect size, q-value, Sample size, Stability, Stratum survival, Population and Run.

**Step 16.** Understand the ordering. **The list is sorted by effect size, largest first** - by how big the relationship is, not by how statistically confident it is.

> **Why this matters to you.** A relationship can be extremely certain and completely unimportant. Sorting by certainty puts trivia at the top of your list. **Sorting by size puts the things worth acting on at the top**, and the product does not let you reverse this.

**Step 17.** Read each column of the first row:

| Column | What it tells you |
|---|---|
| **Effect size** | How strong the relationship is. Bigger matters more |
| **q-value** | The **multiple-testing-adjusted evidence measure**. Smaller values mean stronger evidence after accounting for the many relationships that were tested at the same time. **It is not the probability that the finding is false** |
| **Sample size** | How many units this is based on |
| **Stability** | Whether the result survives when the data is resampled. **A result that is not stable is flagged** |
| **Stratum survival** | Whether it still holds when other differences are accounted for |

**Step 18.** Look for a row marked **not significant**.
*It is shown, deliberately, rather than hidden. "We looked and found nothing" is a real answer and often a valuable one - it stops your team chasing something that is not there.*

**Step 19.** Click any row. A drawer opens on the inline-end with the evidence: the population, the method, the framing, and a link to the actual rows behind it.

**Step 20.** Click through to the rows. **Every figure derived from your plant data can be followed to the rows that produced it**; figures that are configuration or measurement - a quota, a metric, a latency - resolve to their own record instead.

**Step 21.** Optional, and only if you have the Pro Plus tier: go to **ML Readiness and Models** (`/ml-readiness`) in the main menu. You will see a matrix of outcomes against grains, each cell showing how ready it is with real measured numbers.
*A blocked cell on a young installation is normal, and it is shown as a countdown rather than a fault.*

---

**What you achieved in T6.** You created a saved, versioned analysis; the product either produced an evidence-ranked result or told you honestly why it would not; and every figure traces back to your own rows.

---

## 6.8 TUTORIAL T7 - Put your analysis on a schedule and watch it

**Guideline 6.4.7. Journey steps J12 and J15.**

**You will need:** the Analysis Definition from T6, and the right role for the part you are doing:

| Role | What you can do on F4 |
|---|---|
| **Administrator** | **Create a job definition (New definition), delete one, and change its pool, compute weight or target** |
| **Data Engineer** | Edit the schedule, the dependencies and the parameters of a job **that already exists**, and enable or disable it |

> **If you are a Data Engineer**, ask your administrator to create the job definition with its target set, then do steps 9 to 23 yourself. Creating a job commits compute capacity on the server, which is why it is an administrator act rather than an obstruction.

**What you are about to do.** T6 ran your Analysis Definition once, by hand. Now you will attach it to a job so it runs by itself, and learn how to watch it and how to read a refusal.

---

**Step 1.** In the **main menu**, under **Administration**, click **Jobs Administration** (`/admin/jobs`).

> If the page is not in your menu at all, you have neither the Administrator nor the Data Engineer role. Ask your administrator.

**Step 2.** **Administrator only.** In the header, click **New definition**.
*If you are a Data Engineer this button is disabled and says so on hover. Ask your administrator to create the definition and set its target - steps 2 to 8 - then continue yourself from step 9.*

**Step 3.** Give the job a name that says what it does, for example:

```
Weekly quality driver analysis - Line 1
```

**Step 4.** In the definition editor, select the **Schedule and resources** tab.

**Step 5.** Open the **Target definition** selector and choose the **Analysis Definition you saved in T6**.
*The list shows only published definitions of the kind this job class can run, so you cannot attach the wrong sort of thing by accident.*

**Step 5a.** Beside it, set the **Version policy**:

| Choice | What it means |
|---|---|
| **Current published** | The job always runs the newest published version. Republishing a correction takes effect at the next run. **This is the normal choice** |
| **Pinned** | The job keeps running one specific version until a person changes it. Choose this when a result must stay reproducible across a change |

*Whichever you choose, **the version actually used is recorded on every run**, so a result stays explainable.*

**Step 6.** Use the **Schedule editor** to say how often. It is written in plain language, not in code.
*Choose something that matches how quickly your data changes. An analysis over a long window does not need to run every few minutes.*

**Step 7.** Look at the **Pool select**. Leave it as it is unless your administrator has told you otherwise.
*A pool is a queue with a limited number of places. It is what stops a hundred jobs starting at once and overwhelming the server. Changing it asks you to confirm, because it changes what can run at the same time.*

**Step 8.** Look at the **Compute weight**. Leave it as it is.
*Weight says how much of the pool one run occupies. A heavy job takes more places. This too asks you to confirm.*

**Step 9.** Now select the **Dependencies** tab. This is worth doing properly.

**Step 10.** Click **Add dependency**.

**Step 11.** In **Depends-on job**, choose the **canonical refresh job from T4**.
*You are telling the product: do not analyse until the plant data has been refreshed. Without this, your analysis may run on yesterday's data.*

**Step 12.** Set **Dependency kind** to **data**.

**Step 13.** Leave **Required / Optional** on **Required**.
*Required means: if the refresh did not run, do not run the analysis either. That is what you want. Optional would mean: run anyway and note that the upstream did not.*

**Step 14.** Set the **Staleness tolerance** - how old the upstream result may be and still count.
*If the refresh runs every 15 minutes, something like 60 minutes is sensible. Too tight and your analysis will block for no good reason.*

**Step 15.** Click **Validate graph**.
*The product checks that your dependencies do not form a loop. If job A waits for B and B waits for A, neither could ever run, so it is refused with both jobs named.*

**Step 16.** Click **Impact preview** in the header.
*It tells you what else this change affects and what extra load it adds to the pool. **Read it before you save.***

**Step 17.** Save the definition.

**Step 18.** Switch the header toggle from **Table** to **Graph**.
*Your jobs appear as a diagram, coloured by how they last ended, with lines showing what waits for what. Required dependencies are solid lines and optional ones are dashed.*

**Step 19.** Go to **Jobs Monitor** and find your new job. Click **Run now** rather than waiting for the schedule.

**Step 20.** Watch the status pill, then expand the row and read the run log.

**Step 21.** Learn to read the two ways a run can end without producing a result:

| Status | Meaning | What to do |
|---|---|---|
| **Completed** | It ran and produced findings | Go and read them |
| **Blocked** | The readiness gate stopped it, and the row names which dimension and by how much | Wait for more data, or widen the window |
| **Failed** | Something technical went wrong; the row carries a code | Note the code, tell your administrator |

> **A blocked run appears in the monitor as a real run**, with an identifier and a reason. It is not an absence and it is not an error. **The product records the fact that it declined, so you can see it declined and why** - which is very different from a job that silently produced nothing.

**Step 22.** Click **Pause** on the job, then **Resume**.

**Step 23.** Open the **activity tray** from the header while a run is going.
**You know the whole tutorial worked when:** you can start a run, navigate away to another page, and still see its progress in the tray - and when it finishes, the finding appears on the Findings page without you having done anything else.

---

**What you achieved in T7.** Your Analysis Definition now runs by itself, in the right order, without overloading the server, and you can see honestly whether it ran, declined, or failed. *Steps 1 to 18 are J15 - operating and governing the platform - while the run itself belongs to J12.*

---

## 6.9 TUTORIAL T8 - Build a page that shows what the analysis produced

**Guideline 6.4.8. Journey steps J10, J11, J13 and J14.** *You build another page (J10), explore it (J11), read the intelligence (J13), and from step 17 onward you take a human decision on a prediction and see it evaluated (J14).*

**What you are about to do.** In T5 you charted your plant data. Now you will chart the **intelligence** the product produced - findings, and if your tier includes them, predictions and practices.

The important thing to notice is that **there is nothing new to learn.** Intelligence behaves exactly like any other data: you chart it, filter it, compare it and drill into it the same way.

---

**Step 1.** In the **main menu**, click **Page Builder**.

**Step 2.** Click **Create page**. Name it something like:

```
Line 1 Findings and Risk
```

**Step 3.** Choose the audience roles and confirm. The empty grid appears.

**Step 4.** Click **Add widget** and choose **table**. Name it and continue.

**Step 5.** In the authoring panel, open the source selector.
*Look carefully at this list. Alongside your plant entities you will see **intelligence sources**: findings, predictions, prediction drivers, practices, practice drift, remediation candidates, suggestion decisions, value impacts, readiness states.*

**Step 6.** Choose **findings**.

**Step 7.** Choose the columns for your table - outcome, factor, effect size, q-value, sample size, stability.

**Step 8.** Add a saved filter on the widget restricting it to the outcome you analysed in T6.
*A filter saved on a widget is that widget's **permanent scope**. Page filters and clicks narrow it further, inside that scope. They never widen it.*

**Step 9.** Preview, then **Save**.

**Step 10.** Click **Add widget** again and choose **chart**. This time build a bar chart of effect size by factor, from the same findings source.

**Step 11.** Add a third widget, and this is the one worth doing: choose **chart**, and bind **a plant measure and an intelligence measure in the same widget** - for example a process parameter over time with the finding's effect overlaid.

> **If the product refuses this** with a message that no path exists between them, it means the relationship that would connect them has not been declared. Go back to T3 and add it. **The product will not invent a connection between two things you never told it were related.**

**Step 12.** If your tier includes prediction, add a fourth widget bound to **predictions**, showing risk class and the count of units in each.

**Step 13.** Arrange the widgets, then click **Save layout**.

**Step 14.** Open the page in **Interactive Workspace**.

**Step 15.** **Click one value in the findings table** - an outcome, say.
*Watch every widget on the page, including the intelligence ones. They all narrow together, because they are all connected through the same relationship model you declared in T3.*

**Step 16.** Click a row in the findings table to open its evidence drawer, then follow it through to the source rows.

**Step 17.** If you have the prediction tier, go to **Early Warning** in the main menu (`/early-warning`).

**Step 18.** Look at the queue. Each row is a unit currently in process that is predicted to be at risk, ranked, with a **time-remaining** countdown.

**Step 19.** Click a row. The drawer opens with the drivers - which conditions raised the risk, each showing the current value against the normal range.

**Step 20.** Look for a **remediation card**. If one is present, it names a practice, the stage at which to do it, how many historical cases support it, and the expected effect as a range.

> **Read this carefully, because this is a safety boundary and not a display preference.** A remediation card appears only when the candidate has passed **nine separate checks** for **this specific unit at this specific moment**: that the parameter can actually be controlled, that the proposed stage has not passed, that the values stay inside your specifications and operating limits, that **no safety rule forbids the combination**, that enough history supports it, that it survives comparison against confounders, that the effect is more than uncertainty, that the evidence is causal where the data allows, and that the underlying practice is stable.
>
> **What happens to a candidate that fails depends on which check it failed:**
>
> | Outcome | What you see |
> |---|---|
> | **Actionable** | A remediation card in the **Decision** group, with Accept, Reject and Defer |
> | **Evidence only** | A row in the **Investigation** group reading "observed historical difference - not actionable here", with the failed check named. **No decision control of any kind** |
> | **Exploratory** | A row in the **Investigation** group behind a disclosure, with the uncertainty stated. **No decision control of any kind** |
> | **Suppressed** | **Nothing. It is not shown on this page at all.** |
>
> **Suppressed means suppressed.** A candidate that fails the **safety check** is not displayed to you operationally, in any group, at any tier, for any role. It is recorded on the run and in the job log so that it is auditable, and it is recoverable only from there. **The product does not show you a practice that a safety rule forbids, even as evidence**, because a reader under time pressure may act on what they see.
>
> The same rule governs the whole Decision group: **Reject and Defer are gated exactly as Accept is**. Rejecting an observation would record it as though it had been offered as a recommendation, and would corrupt the effectiveness statistics this product exists to produce. For a non-actionable candidate you may **Inspect**, **Compare**, or **Escalate for investigation** - and nothing else.

**Step 21.** If a card is actionable, use the buttons in order: **Acknowledge**, then **Assign**, then **Accept**, then later **Record action** with what was actually done and at which stage.

**Step 22.** Notice a row marked **past actionable stage**. It is struck through, moved below the others, and has no Accept button.
*The product is telling you that the moment to act has gone and this is now historical evidence. It does not pretend otherwise.*

**Step 23.** Later, once the unit has passed the stage, return and click **View evaluation**.
**You know the whole tutorial worked when:** you can see whether the prediction turned out to be correct, and whether the remediation actually helped - **measured from your own data, not asserted.**

---

**What you achieved in T8, and in the whole manual.** You connected a database, chose data, built your plant's model, loaded it, charted it, analysed it, scheduled it, and read the result. **Everything you created is a versioned definition inside the product**, exportable, inspectable, and yours.

---

## 6.10 When something does not work

The most common situations, in the order beginners meet them.

| What you see | What it actually means | What to do |
|---|---|---|
| A button is grey | Something it needs is missing | Hover over it - it says what |
| Read-only verification failed (`CN03`) | The database account can write | Ask for a read-only account. **Do not work around it** |
| Every import reads the whole table | No watermark column was chosen | Go to Prepare Import and choose one |
| Rows were quarantined | Individual rows failed validation | Mapping Health, read the group headings, fix the cause, reprocess |
| A defect code is not recognised (`PV06`) | The vocabulary table has not been imported | Register and import it, then reprocess |
| A line will not connect on the board | The two ends are not compatible | Read the debug log at the block-end - it names the rule |
| Analysis says **Blocked** | Not enough data to answer defensibly | Widen the window, wait, or pick a commoner outcome. **The threshold cannot be lowered** |
| A field is missing from a dropdown | That column has not been mapped | Return to T3 and map it |
| Two things will not chart together | No relationship connects them | Declare it in T3 |
| The assistant refuses | It has no evidence for that question | Rephrase, or ask something the data can answer. **It refuses rather than guessing** |
| Amber, not red | It worked, with something you should know | Read it, then continue |
| A page is missing from the menu | Your role does not include it, or your tier does not | Ask your administrator |
| **Publish version** is disabled | Publishing requires the Administrator role | Your draft is saved; ask an administrator to publish it |
| **New definition** on Jobs Administration is disabled | Creating a job is an Administrator act | Ask an administrator to create it with its target; you can then edit its schedule and dependencies |
| A job will not save (`JB01`) | Its class needs a target definition and none is chosen | Choose the Analysis, Model, Transformation or Report definition it should run |
| The target list is empty or missing what you expect (`JB02`) | The list only offers definitions of the kind that job class can run, and only published ones | Publish the definition first, or check you are editing the right job class |
| No assistant button anywhere | The assistant is Pro Plus and above | Nothing in this manual depends on it |
| A remediation you expected is not shown at all | It was **suppressed** by the safety check | This is deliberate. It is recorded in the run and the job log for audit |

**When in doubt, ask the assistant**, if your tier includes it. It is on every authenticated page, it knows which page you are on, and every figure it gives you carries its evidence.

---

## 6.11 The words this product uses

| Word | What it means here |
|---|---|
| **Connection** | A read-only link to one of your databases |
| **Dataset** | One table, view or file you chose to bring in |
| **Staging** | The holding area where your data sits exactly as it arrived |
| **Plant model** | The product's organised model of your plant, built from what you mapped |
| **Definition** | Anything you create and save: a transformation, a page, a widget, a filter, an analysis. **Always versioned, always stored in the product** |
| **Relationship** | A link you declared between two things, used everywhere afterwards |
| **Genealogy** | Which unit came from which - parents and children |
| **Grain** | What one row means: a piece, a batch, a coil |
| **Job** | Something that runs on a schedule |
| **Run** | One execution of a job |
| **Readiness gate** | The five checks made before any analysis, which can block it |
| **Finding** | A relationship the product found across many units, looking backward |
| **Prediction** | A statement about **one** unit, looking forward |
| **Practice** | A way of operating, reconstructed from your own history |
| **Remediation** | Something to do later that history says may fix a predicted problem |
| **Quarantine** | Where rows go when they cannot be accepted, with the reason |
| **Evidence** | The chain from a figure back to what produced it. For plant-derived figures that chain reaches your own source rows; for configuration and measurement figures it reaches the record that set or measured them |

---

## 6.12 Target audience and voice

**6.5 Target audience.** The person who will configure and use this software. Some software knowledge is helpful and none is required. Every act in this manual is achievable by dragging, clicking and choosing from lists. **SQL authoring is never required in this tutorial.** SQL is mentioned only twice, and in both cases only to explain something you may see: the optional advanced mode on the Mode toggle in T3, and the read-only **Compiled SQL** preview, which exists so that your database administrator can inspect what a diagram will execute.

**6.6 Voice.** Senior product owner: patient, exact, explaining not only which button to press but why the product behaves as it does - particularly when it refuses. A user who understands why the readiness gate blocked their analysis will trust the results it does produce. A user who does not will think the product is broken.

---

*End of Chapter 5. Every page code, route, control label, message and error code used here is specified in Chapter 3 4.4 and 4.5.21. Where this chapter and Chapter 3 disagree, Chapter 3 governs and this chapter is corrected.*

## 6.9a Three rules you will see in real plant analysis — transition, trade-off and period comparison

These are not extra journey steps; they are rules used by T3, T6 and T8.

**Transition / stabilisation.** When your plant declares a product/recipe/tool/campaign/setup or other context transition, you also declare how steady-state resumes: after a time, after a subject count, after a condition, or immediately. When an analysis spans two regimes the product will partition them or show `RG01` rather than quietly mixing them. A result card shows whether its population was Stable, Transition, Stabilising or Mixed.

**Multi-objective practice.** If you ask for “best practice” across more than one objective, choose an Objective Set. If you have not declared how conflicting objectives should be traded, the product shows the supported non-dominated practices and **does not force one winner**. This is expected behaviour, not an error.

**Compare two periods.** In the Assistant or Analysis Toolbox, a period comparison first shows exact differences — transition count, stabilisation exposure, stable-run length, production-impact time, yield/scrap, energy and other registered facts — with evidence. Learned explanation comes after the exact comparison. Assumption-based money values appear only when the Value Engine has the required cost assumptions.

