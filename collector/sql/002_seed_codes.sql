-- seasonal-pick: load the aT code tables
--
-- Run from the repository root, because \copy resolves paths against the
-- client's working directory:
--   psql -d seasonal_pick -v ON_ERROR_STOP=1 -f collector/sql/002_seed_codes.sql
--
-- Source files are the CSV exports kept verbatim under collector/seed/codes.
-- They are never edited: anything aT does not provide, such as a region's
-- administrative level, is assigned here instead.
--
-- Re-running this file is safe. Each table is staged into a temporary copy
-- and merged, so a refreshed CSV adds new codes and updates changed names
-- without deleting anything that already references them.

BEGIN;

-- ============================================================
-- Trade types and categories (no dependencies)
-- ============================================================

CREATE TEMP TABLE _trade_types (LIKE trade_types) ON COMMIT DROP;
\copy _trade_types from 'collector/seed/codes/trade_type.csv' with (format csv, header true)
INSERT INTO trade_types SELECT * FROM _trade_types
    ON CONFLICT (se_cd) DO UPDATE SET se_nm = EXCLUDED.se_nm;

CREATE TEMP TABLE _categories (LIKE categories) ON COMMIT DROP;
\copy _categories from 'collector/seed/codes/category.csv' with (format csv, header true)
INSERT INTO categories SELECT * FROM _categories
    ON CONFLICT (ctgry_cd) DO UPDATE SET ctgry_nm = EXCLUDED.ctgry_nm;

-- ============================================================
-- The item hierarchy, loaded parent first so the foreign keys hold
-- ============================================================

CREATE TEMP TABLE _items (LIKE items) ON COMMIT DROP;
\copy _items from 'collector/seed/codes/item.csv' with (format csv, header true)
INSERT INTO items SELECT * FROM _items
    ON CONFLICT (ctgry_cd, item_cd) DO UPDATE SET item_nm = EXCLUDED.item_nm;

CREATE TEMP TABLE _varieties (LIKE varieties) ON COMMIT DROP;
\copy _varieties from 'collector/seed/codes/variety.csv' with (format csv, header true)
INSERT INTO varieties SELECT * FROM _varieties
    ON CONFLICT (ctgry_cd, item_cd, vrty_cd) DO UPDATE SET vrty_nm = EXCLUDED.vrty_nm;

CREATE TEMP TABLE _grades (LIKE grades) ON COMMIT DROP;
\copy _grades from 'collector/seed/codes/grade.csv' with (format csv, header true)
INSERT INTO grades SELECT * FROM _grades
    ON CONFLICT (ctgry_cd, item_cd, vrty_cd, grd_cd) DO UPDATE SET grd_nm = EXCLUDED.grd_nm;

-- ============================================================
-- Regions
--
-- The CSV has two columns; `level` is ours. The merge below lists the two
-- data columns explicitly so that re-running never overwrites a level that
-- was assigned here.
-- ============================================================

CREATE TEMP TABLE _regions (LIKE regions) ON COMMIT DROP;
\copy _regions (sgg_cd, sgg_nm) from 'collector/seed/codes/region.csv' with (format csv, header true)
INSERT INTO regions (sgg_cd, sgg_nm) SELECT sgg_cd, sgg_nm FROM _regions
    ON CONFLICT (sgg_cd) DO UPDATE SET sgg_nm = EXCLUDED.sgg_nm;

-- Levels are assigned by hand because the code pattern does not carry them:
-- 3201 (Gangwon, a province) and 1101 (Seoul, a metropolitan city) both end
-- in 01, and the provinces are otherwise numbered x00.
UPDATE regions SET level = 'metro' WHERE sgg_cd IN (
    '1101',  -- 서울
    '2100',  -- 부산
    '2200',  -- 대구
    '2300',  -- 인천
    '2401',  -- 광주
    '2501',  -- 대전
    '2601',  -- 울산
    '2701'   -- 세종
);

UPDATE regions SET level = 'province' WHERE sgg_cd IN (
    '3100',  -- 경기
    '3201',  -- 강원
    '3300',  -- 충북
    '3400',  -- 충남
    '3500',  -- 전북
    '3600',  -- 전남
    '3700',  -- 경북
    '3800',  -- 경남
    '3911'   -- 제주. The only 39xx code, so it stands for the whole region;
             -- nothing can be double-counted against it either way.
);

UPDATE regions SET level = 'special' WHERE sgg_cd IN (
    '9998',  -- 온라인
    '1000'   -- 전국(축산부류한정)
);

-- Everything left is a city inside one of the provinces above.
UPDATE regions SET level = 'city' WHERE level IS NULL;

-- Fail loudly if the code table gained a region nobody has classified,
-- instead of letting it take a plausible-looking default.
DO $$
DECLARE n integer;
BEGIN
    SELECT count(*) INTO n FROM regions WHERE level IS NULL;
    IF n > 0 THEN
        RAISE EXCEPTION 'regions.level is unassigned for % row(s)', n;
    END IF;
END $$;

COMMIT;

-- Expected: 4, 6, 136, 332, 693, 38
SELECT 'trade_types' AS table_name, count(*) FROM trade_types
UNION ALL SELECT 'categories',  count(*) FROM categories
UNION ALL SELECT 'items',       count(*) FROM items
UNION ALL SELECT 'varieties',   count(*) FROM varieties
UNION ALL SELECT 'grades',      count(*) FROM grades
UNION ALL SELECT 'regions',     count(*) FROM regions;

SELECT level, count(*) FROM regions GROUP BY level ORDER BY level;
