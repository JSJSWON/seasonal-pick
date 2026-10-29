-- 006_add_chicken_target.sql
--
-- Restores 축산물 to the MVP, which had been five categories since 005
-- deactivated 닭고기. The category was never gone from the API - the item
-- code changed. 515 닭고기 / 02 도계 stops in 2023; 9901 닭 / 99 육계(kg)
-- has been surveyed every day since 2022-01-13 (3.2-타).
--
-- Two things had to be settled before this row could be added, and both are:
--   - livestock regions are metros and provinces, not cities, so its region
--     set barely overlaps the crops'. The screen picks an item first, then
--     that item's regions (1.3), so the mismatch does not surface.
--   - monthly figures now come from price_daily for every target alike, so
--     nothing forks just because this category lacks a monthly series.
--
-- Verified against the API before writing this file (서울, 2026-10):
--   unit kg · unit_sz 1 · 28 rows for 10/01..10/28, weekends included
--   2026 to date: 301 days, 3,436~6,682원/kg, 36 distinct prices
--   min = avg = max on every single day - a day carries one price, unlike
--   the crops where the three differ daily. Trend and verdict still work;
--   a "최저~최고" range cannot be shown for this target.

BEGIN;

INSERT INTO tracked_targets (ctgry_cd, item_cd, vrty_cd, grd_cd, se_cd, display_nm) VALUES
    ('500', '9901', '99', '1', '01', '닭 (육계 1kg)')
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
    ORDER BY t.is_active DESC, c.ctgry_cd, t.id;
