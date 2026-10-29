-- 005_update_targets.sql
--
-- Two of the nine targets seeded in 003 turned out to carry no usable data.
-- Both were found by querying perYearMonth/price for each target before any
-- backfill was written; a foreign key only proves a combination exists, not
-- that aT publishes prices for it.
--
-- Rows are deactivated rather than deleted. The record of "this combination
-- was considered and rejected, for this reason" is what stops the same
-- candidate being picked again later, and collection_log references target
-- ids, so ids must stay resolvable.

BEGIN;

-- 닭고기 (도계): retail prices for the whole livestock category stop in 2022.
--   2021: 4,048 rows   2022: 1,252   2023-2026: 0
-- Not a code problem. Grade 07/12, variety 9901 닭, every sgg_cd including
-- 1000 (전국, 축산부류한정), and perRegion/price all return nothing for 2026.
-- Only 친환경 (se_cd 03/07) still carries a handful of rows, which is not a
-- supermarket price. There is no livestock substitute in this API.
UPDATE tracked_targets SET is_active = false
    WHERE (ctgry_cd, item_cd, vrty_cd, grd_cd, se_cd) = ('500', '515', '02', '05', '01');

-- 김 (마른김): published under grade code '30', not the '05' that the grade
-- workbook lists. '05' returns 0 rows; '30' returns 886 rows covering all 60
-- months and 18 regions. 004 supplies the missing code.
UPDATE tracked_targets SET is_active = false
    WHERE (ctgry_cd, item_cd, vrty_cd, grd_cd, se_cd) = ('600', '641', '00', '05', '01');

INSERT INTO tracked_targets (ctgry_cd, item_cd, vrty_cd, grd_cd, se_cd, display_nm) VALUES
    ('600', '641', '00', '30', '01', '김 (마른김 10장)')
ON CONFLICT (ctgry_cd, item_cd, vrty_cd, grd_cd, se_cd)
    DO UPDATE SET display_nm = EXCLUDED.display_nm, is_active = true;

COMMIT;

-- The collection targets as they now stand.
SELECT t.id, c.ctgry_nm AS 부류, i.item_nm AS 품목, v.vrty_nm AS 품종,
       g.grd_nm AS 등급, s.se_nm AS 구분, t.display_nm AS 표시명, t.is_active AS 활성
    FROM tracked_targets t
    JOIN categories c USING (ctgry_cd)
    JOIN items i USING (ctgry_cd, item_cd)
    JOIN varieties v USING (ctgry_cd, item_cd, vrty_cd)
    JOIN grades g USING (ctgry_cd, item_cd, vrty_cd, grd_cd)
    JOIN trade_types s USING (se_cd)
    ORDER BY t.is_active DESC, t.id;
