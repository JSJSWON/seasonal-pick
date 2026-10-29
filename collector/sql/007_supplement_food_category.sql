-- 007_supplement_food_category.sql
--
-- A seventh category. aT's code workbooks list six (식량작물 · 채소류 ·
-- 특용작물 · 과일류 · 축산물 · 수산물), but the API also serves
-- `800 식품` - branded processed goods such as 두부, 김치 and 콩나물.
--
-- 004 missed this because the gap scan enumerated categories 100..600,
-- which are the ones our code table knows. Looking only where the code
-- table points cannot find a category the code table omits. The scan was
-- redone without enumerating: every row for a region-month was fetched and
-- its categories counted. Seoul 2026-10 returned 3,637 rows across seven
-- categories, 144 of them 800.
--
-- Provenance: read from perRegion/price responses on 2026-10-30
-- (서울·부산·제주 × 2026-01, 05, 10). Values are verbatim, names included.
-- 800 reaches back to 2022 (Seoul 2022-01: 160 rows; 2020-01: none).
--
-- Not a collection target. These are specific products with a brand and a
-- pack size in the variety name, which is a different kind of thing from
-- 배추 가을 상품. Whether any belong in the MVP is a separate decision; the
-- codes are recorded here so a future target is not blocked by a foreign
-- key, and so the next gap scan starts from a complete table.

BEGIN;

INSERT INTO categories (ctgry_cd, ctgry_nm) VALUES
    ('800', '식품')
ON CONFLICT (ctgry_cd) DO NOTHING;

INSERT INTO items (ctgry_cd, item_cd, item_nm) VALUES
    ('800', '811', '즉석밥'),
    ('800', '812', '두부'),
    ('800', '813', '김치'),
    ('800', '814', '고추장'),
    ('800', '815', '된장'),
    ('800', '816', '간장'),
    ('800', '817', '맛김(조미김)'),
    ('800', '818', '콩나물')
ON CONFLICT (ctgry_cd, item_cd) DO NOTHING;

INSERT INTO varieties (ctgry_cd, item_cd, vrty_cd, vrty_nm) VALUES
    ('800', '811', '01', 'CJ 햇반 백미밥 210g×8개'),
    ('800', '812', '01', '풀무원 국산콩 300g×2'),
    ('800', '813', '02', '대상 종가집 포기김치 2.8kg'),
    ('800', '814', '01', '해찬들 우리쌀로만든 태양초고추장 1kg'),
    ('800', '815', '01', '청정원 재래식 생된장 1kg'),
    ('800', '816', '01', '샘표 양조간장 501 1.7L'),
    ('800', '817', '01', '동원에프앤비 양반 들기름김 5g×20봉'),
    ('800', '818', '01', '풀무원 국산콩 콩나물 340g')
ON CONFLICT (ctgry_cd, item_cd, vrty_cd) DO NOTHING;

INSERT INTO grades (ctgry_cd, item_cd, vrty_cd, grd_cd, grd_nm) VALUES
    ('800', '811', '01', '04', '상품'),
    ('800', '812', '01', '04', '상품'),
    ('800', '813', '02', '04', '상품'),
    ('800', '814', '01', '04', '상품'),
    ('800', '815', '01', '04', '상품'),
    ('800', '816', '01', '04', '상품'),
    ('800', '817', '01', '04', '상품'),
    ('800', '818', '01', '04', '상품')
ON CONFLICT (ctgry_cd, item_cd, vrty_cd, grd_cd) DO NOTHING;

COMMIT;

SELECT c.ctgry_nm AS 부류, i.item_nm AS 품목, v.vrty_nm AS 품종, g.grd_nm AS 등급
    FROM grades g
    JOIN varieties v USING (ctgry_cd, item_cd, vrty_cd)
    JOIN items i USING (ctgry_cd, item_cd)
    JOIN categories c USING (ctgry_cd)
    WHERE g.ctgry_cd = '800'
    ORDER BY i.item_cd;
