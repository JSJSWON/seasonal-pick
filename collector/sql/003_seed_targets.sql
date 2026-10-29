-- seasonal-pick: the MVP collection targets
--
--   psql -d seasonal_pick -v ON_ERROR_STOP=1 -f collector/sql/003_seed_targets.sql
--
-- One item per aT category, chosen so that the six categories are all
-- exercised rather than only the easy ones. Cabbage contributes four rows
-- because its varieties are a seasonal relay - spring, summer (highland),
-- autumn and winter are separate crops that follow one another.
--
-- Grade is per target, not a project-wide constant: "04" (상품) does not
-- exist for chicken or laver, where the surveyed grade is "05" (중품).
--
-- Re-running this file is safe; it updates the display names in place.
--
-- The foreign key to `grades` is what validates these codes, so 002 must
-- have run first.

BEGIN;

INSERT INTO tracked_targets (ctgry_cd, item_cd, vrty_cd, grd_cd, se_cd, display_nm) VALUES
    ('100', '111', '01', '04', '01', '쌀 (20kg)'),
    ('200', '211', '01', '04', '01', '배추 (봄)'),
    ('200', '211', '02', '04', '01', '배추 (여름·고랭지)'),
    ('200', '211', '03', '04', '01', '배추 (가을)'),
    ('200', '211', '06', '04', '01', '배추 (월동)'),
    ('300', '317', '00', '04', '01', '새송이버섯'),
    ('400', '413', '01', '04', '01', '복숭아 (백도)'),
    ('500', '515', '02', '05', '01', '닭고기 (도계)'),
    ('600', '641', '00', '05', '01', '김 (마른김)')
ON CONFLICT (ctgry_cd, item_cd, vrty_cd, grd_cd, se_cd)
    DO UPDATE SET display_nm = EXCLUDED.display_nm;

COMMIT;

-- Resolve every target against the code tables. A foreign key only proves
-- the combination exists; this shows which one it actually is, so a typo
-- that still happens to be a valid code cannot pass unnoticed.
SELECT t.id,
       c.ctgry_nm AS 부류,
       i.item_nm  AS 품목,
       v.vrty_nm  AS 품종,
       g.grd_nm   AS 등급,
       s.se_nm    AS 구분,
       t.display_nm AS 표시명
FROM tracked_targets t
JOIN categories  c USING (ctgry_cd)
JOIN items       i USING (ctgry_cd, item_cd)
JOIN varieties   v USING (ctgry_cd, item_cd, vrty_cd)
JOIN grades      g USING (ctgry_cd, item_cd, vrty_cd, grd_cd)
JOIN trade_types s USING (se_cd)
ORDER BY t.id;
