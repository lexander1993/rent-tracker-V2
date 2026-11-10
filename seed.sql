-- Sample data: 5 rows in each table.
-- Run with Docker:  Get-Content seed.sql | docker exec -i rentdb-pg psql -U postgres -d rentdb_v2
-- Rows are linked by name, not id, so this works on a database that already has data.
-- It stops if the sample properties are already there, so running it twice adds nothing.

BEGIN;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM "Properties" WHERE "Name" = 'Maple House') THEN
        RAISE EXCEPTION 'Sample data is already loaded.';
    END IF;
END $$;

INSERT INTO "Properties" ("Name", "Address") VALUES
    ('Maple House',   '12 Maple St, Burwood NSW 2134'),
    ('Harbour View',  '8/40 Bay Rd, Waverton NSW 2060'),
    ('Oak Cottage',   '3 Oak Ave, Epping NSW 2121'),
    ('Riverside Flat','21 River Pde, Parramatta NSW 2150'),
    ('Sunset Villa',  '77 Ocean St, Bondi NSW 2026');

INSERT INTO "Rooms" ("PropertyId", "Name", "Status", "AskingRent")
SELECT p."Id", v.room, v.status, v.rent
FROM (VALUES
    ('Maple House',    'Room 1', 'AVAILABLE', 1200.00),
    ('Harbour View',   'Room 1', 'AVAILABLE', 1450.00),
    ('Oak Cottage',    'Room 1', 'AVAILABLE', 1100.00),
    ('Riverside Flat', 'Room 1', 'AVAILABLE', 1300.00),
    ('Sunset Villa',   'Room 1', 'REPAIR',    1600.00)
) AS v(property, room, status, rent)
JOIN "Properties" p ON p."Name" = v.property;

INSERT INTO "Renters" ("Name", "Dob", "Status", "CreatedAt") VALUES
    ('Emily Chen',    DATE '1994-03-15', 'CURRENT',   now()),
    ('James Wilson',  DATE '1988-07-22', 'CURRENT',   now()),
    ('Priya Sharma',  DATE '1997-11-05', 'CURRENT',   now()),
    ('Lucas Nguyen',  DATE '1991-01-30', 'CURRENT',   now()),
    ('Sophie Taylor', DATE '1985-09-12', 'MOVED_OUT', now());

-- Four current tenancies and one that has ended (Sophie, whose room is now under repair).
INSERT INTO "Tenancies" ("RoomId", "RenterId", "StartDate", "EndDate", "MonthlyRent", "DueDay")
SELECT ro."Id", re."Id", v.start_date, v.end_date, v.rent, v.due_day
FROM (VALUES
    ('Maple House',    'Emily Chen',    DATE '2026-01-01', NULL::date,        1200.00, 1),
    ('Harbour View',   'James Wilson',  DATE '2026-03-01', NULL::date,        1450.00, 5),
    ('Oak Cottage',    'Priya Sharma',  DATE '2026-06-15', NULL::date,        1100.00, 15),
    ('Riverside Flat', 'Lucas Nguyen',  DATE '2026-08-01', NULL::date,        1300.00, 1),
    ('Sunset Villa',   'Sophie Taylor', DATE '2025-10-01', DATE '2026-09-30', 1600.00, 1)
) AS v(property, renter, start_date, end_date, rent, due_day)
JOIN "Properties" p ON p."Name" = v.property
JOIN "Rooms" ro ON ro."PropertyId" = p."Id" AND ro."Name" = 'Room 1'
JOIN "Renters" re ON re."Name" = v.renter;

-- Lucas has only part-paid November, so the app shows a PARTIAL payment.
INSERT INTO "Payments" ("RenterId", "Month", "Amount", "PaidDate")
SELECT re."Id", v.month, v.amount, v.paid_date
FROM (VALUES
    ('Emily Chen',    '2026-11', 1200.00, DATE '2026-11-01'),
    ('James Wilson',  '2026-11', 1450.00, DATE '2026-11-04'),
    ('Priya Sharma',  '2026-10', 1100.00, DATE '2026-10-15'),
    ('Lucas Nguyen',  '2026-11',  650.00, DATE '2026-11-03'),
    ('Sophie Taylor', '2026-09', 1600.00, DATE '2026-09-01')
) AS v(renter, month, amount, paid_date)
JOIN "Renters" re ON re."Name" = v.renter;

INSERT INTO "Expenses" ("PropertyId", "Date", "Category", "Amount", "Note")
SELECT p."Id", v.expense_date, v.category, v.amount, v.note
FROM (VALUES
    ('Maple House',    DATE '2026-10-12', 'Repairs & maintenance',   320.00, 'Fixed leaking kitchen tap'),
    ('Harbour View',   DATE '2026-10-01', 'Insurance',               890.00, 'Annual landlord insurance'),
    ('Oak Cottage',    DATE '2026-09-20', 'Rates & land tax',        610.50, 'Council rates Q2'),
    ('Riverside Flat', DATE '2026-11-02', 'Agent / management fees', 143.00, 'November management fee'),
    ('Sunset Villa',   DATE '2026-10-18', 'Cleaning',                250.00, 'End of lease clean')
) AS v(property, expense_date, category, amount, note)
JOIN "Properties" p ON p."Name" = v.property;

COMMIT;
