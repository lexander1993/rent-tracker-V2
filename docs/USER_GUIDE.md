# Rent Tracker — User Guide

Rent Tracker keeps your rental records in one place: properties and rooms, tenants, tenancies, rent payments and expenses. It turns them into a dashboard, tenant statements and profit reports.

Open the app at **http://127.0.0.1:3001** once it is running (see [TECHNOLOGY.md](TECHNOLOGY.md#running-the-app)).

## Contents

1. [Getting started: the usual order](#getting-started-the-usual-order)
2. [Dashboard](#1-dashboard)
3. [Add tenant](#2-add-tenant)
4. [Record payment](#3-record-payment)
5. [Payment History](#4-payment-history)
6. [Statement](#5-statement)
7. [Properties & rooms](#6-properties--rooms)
8. [Expenses](#7-expenses)
9. [Report](#8-report)
10. [How the app counts rent](#how-the-app-counts-rent)
11. [Known limitations](#known-limitations)

---

## Getting started: the usual order

For a new setup, work through the pages in this order:

1. **Properties & rooms**: add each property, then its rooms.
2. **Add tenant**: add each tenant.
3. **Properties & rooms**: open a room with **Update** and assign a tenant. This creates a *tenancy* (who lives where, from when, at what rent, due on which day).
4. **Record payment**: record rent as it comes in.
5. **Expenses**: record costs such as repairs, insurance and rates.
6. Use **Dashboard**, **Payment History**, **Statement** and **Report** to see where things stand.

All amounts are in dollars ($). Amount boxes accept cents, for example `610.50`.

---

## 1. Dashboard

An overview of one month. Change the month with the **Month** picker at the top.

| Section | What it shows |
|---|---|
| **Overview tiles** | **Rent due** this month and how many tenants owe it. **Rent collected** and the share of rent due it covers. **Paid in full**: the share of tenants who have paid everything. **Occupancy**: rooms with a tenant out of all rooms. |
| **Rent due this week** | Tenants whose rent falls due in the next 7 days, with the amount still to pay and their status. |
| **Occupancy by residence** | A ring per property showing occupied rooms and how many are vacant. |
| **Rent heatmap · last 12 months** | One row per tenant, one square per month. Green = paid, amber = partly paid, red = unpaid, blank = not living there that month. |
| **Overdue payments** | Tenants whose due date has passed and who still owe money, with days late and amount owed. The most overdue are listed first. |
| **By property** | Rooms, occupied rooms, occupancy, rent due and rent collected for each property. |

---

## 2. Add tenant

Add and manage the people who rent from you.

**Add a tenant**

1. Enter the **Name** and **Date of birth** (both required).
2. Click **Add tenant**.

New tenants are not in a room yet. Assign them a room on **Properties & rooms**.

**All tenants** lists everyone with their date of birth, status and current room ("Not assigned" if they have no room). Use the **Status** filter to show All, Current tenants or Moved-out tenants.

- **Edit** changes a tenant's name, date of birth or status.
- **Delete** removes a tenant. This only works if they have no tenancies and no payments, so history is never lost.
- **Status** updates itself: a tenant is *Current tenant* while they have a tenancy that hasn't ended, and *Moved out* once all their tenancies have ended.

---

## 3. Record payment

Record rent payments and correct them.

**Record a payment**

1. Choose the **Tenant**.
2. Choose **For month**: the rent month this payment pays for, which may differ from the day it was paid.
3. Enter **Amount paid ($)**. Part payments are fine. Several payments for the same month add up.
4. Set **Date paid** (defaults to today).
5. Click **Save payment**.

**All payments** lists payments for the chosen **Month**, with a total at the bottom. Click **Show all months** to see every payment.

- **Edit** opens the payment so you can change the tenant, month, amount or date.
- **Delete** removes the payment after you confirm.

---

## 4. Payment History

**Rent status for the month**: who has paid for the chosen month.

Each row shows the room, tenant, rent due, amount paid and a status:

- **PAID**: paid in full (or more).
- **PARTIAL**: something paid, but less than the rent.
- **NOT PAID**: nothing paid yet for that month.

Change the month with the month picker to look back or ahead.

---

## 5. Statement

A printable rent statement for one tenant, for example to send to them or keep on file.

1. Choose the **Tenant**.
2. Set the **From** and **To** dates, or click **This financial year**, **Last financial year** or **This calendar year**. A financial year runs from 1 July to 30 June.
3. The statement shows:
   - the tenant's name, date of birth, move-in date and the date issued
   - their **tenancies** in that period: room, moved in, moved out ("Current" if still there), monthly rent and due day
   - the **payments received**: which month each was for, date paid and amount
4. Click **Print statement** to print it or save it as a PDF from your browser.

---

## 6. Properties & rooms

Set up your properties and rooms, and manage who lives in each room.

### Properties

1. Enter a **Property name** and, if you like, an **Address**.
2. Click **Add property**.

The list shows each property with its address and number of rooms. **Delete** only works for a property with no rooms and no expenses.

### Room map

A tile for every room, grouped by property, showing its tenant and colour-coded status. Click a tile to open that room's update panel.

| Colour | Meaning |
|---|---|
| Grey: **Vacant** | No tenant. |
| Purple: **Moving in** | A tenant is booked but hasn't moved in yet. |
| Green: **Paid** | This month's rent is paid in full. |
| Blue: **Unpaid** | Not paid yet, and the due date is more than 7 days away. |
| Amber: **Due soon** | Not fully paid, and due within 7 days. |
| Red: **Overdue** | The due date has passed and rent isn't fully paid. |

### Rooms

**Add a room**

1. Choose the **Property**.
2. Enter the **Room name or number**. It must be unique within that property.
3. Enter a **Rental price ($)** if you know it. The asking rent is used to work out how much rent is lost while a room is vacant.
4. Click **Add room**.

The rooms table shows each room's current tenant, move-in date, monthly rent and due date. Click **Update** to open a room's panel:

**If the room is vacant: assign a tenant**

1. Choose the **Tenant**. Only tenants without a room are listed.
2. Set **Moves in**, **Monthly rent ($)** and **Rent due day (1–28)**.
3. Click **Assign tenant**.

**If the room has a tenant**

- **Current tenancy**: change the move-in date, monthly rent or due day, then **Save changes**.
- **End tenancy**: pick the **End on** date and click **End tenancy** when someone moves out. The room becomes vacant after that date.
- **Change tenant**: choose the **New tenant**, their move-in date, rent and due day, then click **Change tenant**. The current tenancy ends on the new tenant's move-in date, and the new one starts that day.

**Delete** removes a room. Its past tenancy records are removed, but rent payments are kept because they belong to the tenant.

The app won't let two tenancies in the same room overlap.

---

## 7. Expenses

Record the costs of running your properties.

**Add an expense**

1. Choose the **Property**.
2. Set **Date paid**.
3. Choose a **Category**: Repairs & maintenance, Insurance, Rates & land tax, Agent / management fees, Utilities, Cleaning, Advertising, Legal & accounting, or Other.
4. Enter the **Amount ($)** and an optional **Note**.
5. Click **Add expense**.

The table lists expenses for the chosen **financial year** (1 July – 30 June; change it with the year picker), with a total at the bottom. **Delete** removes an expense after you confirm.

---

## 8. Report

Income and profit summaries for any date range, for your own records or your accountant.

Set **From** and **To** dates, then choose a tab:

| Tab | What it shows |
|---|---|
| **Profit** | Per property: rent charged, rent collected, arrears (still owed), expenses and profit, plus totals. |
| **Monthly income** | Per month: rent due, rent collected, still owed, expenses and profit, plus totals. |
| **Expenses** | Total expenses and number of expenses, broken down **by category** and **by property**, plus the full list. |

**Download CSV** saves the current tab as a spreadsheet file that opens in Excel or Google Sheets.

The report is a working summary, not a tax return. Check depreciation, capital works and other adjustments with your accountant.

---

## How the app counts rent

- **Rent due** counts the full monthly rent for every calendar month a tenancy covers, even part of it. A tenant who moves in on 15 June owes rent for June.
- **Rent collected** is the total of payments recorded *for* that month (the **For month** field), not the date they were paid.
- **Profit** = rent collected − expenses (cash basis).
- **Arrears / still owed** = rent due − rent collected.
- **Expenses** count by the date they were paid.
- **Due dates**: a due day past the end of a month uses the month's last day.

---

## Known limitations

- **Tenant ID photos** aren't available yet. The **Upload ID** button and the ID photo field on **Add tenant** won't work until the photo feature is moved to the new server.
- **Room status** (Available / Under repair / Not for rent) can't be changed in the app. It's stored on each room, and only *Available* vacant rooms count towards vacancy figures.
