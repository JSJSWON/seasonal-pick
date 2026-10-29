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

-- 닭고기 (도계): this combination is gone from both APIs.
--   perYearMonth, whole 500 category:  2021: 4,048   2022: 1,252   2023+: 0
--   perRegion, 515/02/05, Seoul:       2021:   248   2023+: 0
--
-- Livestock itself is not gone: perRegion still serves 1,845 rows for Seoul
-- over 2026-09..10, but under different item codes (4301 소, 4304 돼지,
-- 4401/4402 수입육, 9901 닭, 9903 계란, 9908 우유). 9901 닭 / 99 육계(kg) has
-- run daily since mid-2022. What livestock has lost is the monthly series,
-- and the verdict's baseline comes from there, so a livestock target could
-- show a current price but never "cheaper than usual". That is the reason it
-- stays out of the MVP rather than being swapped for another code.
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
