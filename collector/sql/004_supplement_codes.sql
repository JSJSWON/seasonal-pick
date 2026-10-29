-- 004_supplement_codes.sql
--
-- Codes that the aT API returns in its responses but that aT's own code
-- tables (collector/seed/codes/*.csv, taken from the portal workbooks) do
-- not list. Without these rows the foreign keys on tracked_targets reject
-- collection targets whose data exists and is being served.
--
-- Example: 김 마른김 is published under grade code '30' for all 60 months
-- of 2021-11..2026-10, yet the grade workbook lists only '05' for it.
--
-- Provenance: every row below was read from a perYearMonth/price response,
-- names included; nothing here is inferred. Sampled 2026-10-30 over
-- 202511..202610 for categories 100/200/300/400/600 plus 202111..202610 for
-- item 641, 35,664 rows in all. The responses were checked for consistency
-- first: each (category, item, variety, grade) carried exactly one name and
-- one unit, and no code changed over the six years sampled.
--
-- ON CONFLICT DO NOTHING, not DO UPDATE: if the portal later publishes these
-- codes, 002 loads the official names and they must win over ours.
--
-- Append-only. More codes will surface as other categories are collected;
-- they go into a new numbered file, not into this one.
--
-- Two notes added after the fact:
--   - the scan enumerated categories 100..600, taken from our own code table,
--     so it could not find `800 식품`, which the table omits entirely. 007
--     adds it. Enumerating from the code table cannot reveal what the code
--     table is missing.
--   - 36 of these 37 combinations were seen again in perRegion/price, the
--     only API still in use. The exception is 600/663/01/30 (전어), which is
--     sparse - 65 rows over five years - and did not fall in the sampled
--     months.

BEGIN;

-- Items absent from item.csv
INSERT INTO items (ctgry_cd, item_cd, item_nm) VALUES
    ('100', '115', '현미'),
    ('400', '437', '키위'),
    ('600', '664', '낙지'),
    ('600', '665', '꼬막')
ON CONFLICT (ctgry_cd, item_cd) DO NOTHING;

-- Varieties absent from variety.csv
INSERT INTO varieties (ctgry_cd, item_cd, vrty_cd, vrty_nm) VALUES
    ('100', '113', '01', '국산'),
    ('100', '115', '01', '찰현미'),
    ('100', '121', '04', '찰보리'),
    ('400', '411', '08', '시나노골드'),
    ('400', '429', '02', '수입(냉동)'),
    ('400', '437', '01', '골드'),
    ('600', '664', '00', '수입(냉동)'),
    ('600', '665', '00', '국산(새꼬막)')
ON CONFLICT (ctgry_cd, item_cd, vrty_cd) DO NOTHING;

-- Grades absent from grade.csv. Grade name '-' is what aT sends; it is kept
-- verbatim rather than replaced with an invented label.
INSERT INTO grades (ctgry_cd, item_cd, vrty_cd, grd_cd, grd_nm) VALUES
    ('100', '113', '01', '04', '상품'),
    ('100', '115', '01', '04', '상품'),
    ('100', '121', '04', '04', '상품'),
    ('400', '411', '08', '04', '상품'),
    ('400', '411', '08', '05', '중품'),
    ('400', '429', '02', '04', '상품'),
    ('400', '437', '01', '04', '상품'),
    ('400', '437', '01', '05', '중품'),
    ('600', '611', '03', '30', '-'),
    ('600', '612', '01', '30', '-'),
    ('600', '619', '03', '20', '大'),
    ('600', '619', '04', '20', '大'),
    ('600', '640', '00', '30', '-'),
    ('600', '641', '00', '30', '-'),
    ('600', '641', '01', '30', '-'),
    ('600', '642', '00', '30', '-'),
    ('600', '644', '00', '30', '-'),
    ('600', '649', '04', '30', '-'),
    ('600', '650', '00', '30', '-'),
    ('600', '651', '00', '30', '-'),
    ('600', '652', '00', '30', '-'),
    ('600', '653', '00', '30', '-'),
    ('600', '654', '01', '30', '-'),
    ('600', '656', '00', '30', '-'),
    ('600', '656', '02', '30', '-'),
    ('600', '656', '04', '30', '-'),
    ('600', '656', '05', '30', '-'),
    ('600', '658', '01', '30', '-'),
    ('600', '658', '02', '30', '-'),
    ('600', '659', '01', '30', '-'),
    ('600', '660', '01', '30', '-'),
    ('600', '661', '00', '30', '-'),
    ('600', '662', '00', '30', '-'),
    ('600', '662', '01', '30', '-'),
    ('600', '663', '01', '30', '-'),
    ('600', '664', '00', '30', '-'),
    ('600', '665', '00', '30', '-')
ON CONFLICT (ctgry_cd, item_cd, vrty_cd, grd_cd) DO NOTHING;

COMMIT;

-- The supplemented rows, for review.
SELECT g.ctgry_cd AS 부류, i.item_nm AS 품목, v.vrty_nm AS 품종,
       g.grd_cd AS 등급코드, g.grd_nm AS 등급명
    FROM grades g
    JOIN varieties v USING (ctgry_cd, item_cd, vrty_cd)
    JOIN items i USING (ctgry_cd, item_cd)
    WHERE (g.ctgry_cd, g.item_cd, g.vrty_cd, g.grd_cd) IN (
        ('100', '113', '01', '04'),
        ('100', '115', '01', '04'),
        ('100', '121', '04', '04'),
        ('400', '411', '08', '04'),
        ('400', '411', '08', '05'),
        ('400', '429', '02', '04'),
        ('400', '437', '01', '04'),
        ('400', '437', '01', '05'),
        ('600', '611', '03', '30'),
        ('600', '612', '01', '30'),
        ('600', '619', '03', '20'),
        ('600', '619', '04', '20'),
        ('600', '640', '00', '30'),
        ('600', '641', '00', '30'),
        ('600', '641', '01', '30'),
        ('600', '642', '00', '30'),
        ('600', '644', '00', '30'),
        ('600', '649', '04', '30'),
        ('600', '650', '00', '30'),
        ('600', '651', '00', '30'),
        ('600', '652', '00', '30'),
        ('600', '653', '00', '30'),
        ('600', '654', '01', '30'),
        ('600', '656', '00', '30'),
        ('600', '656', '02', '30'),
        ('600', '656', '04', '30'),
        ('600', '656', '05', '30'),
        ('600', '658', '01', '30'),
        ('600', '658', '02', '30'),
        ('600', '659', '01', '30'),
        ('600', '660', '01', '30'),
        ('600', '661', '00', '30'),
        ('600', '662', '00', '30'),
        ('600', '662', '01', '30'),
        ('600', '663', '01', '30'),
        ('600', '664', '00', '30'),
        ('600', '665', '00', '30')
    )
    ORDER BY 1, 2, 3, 4;
