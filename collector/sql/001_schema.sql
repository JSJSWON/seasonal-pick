-- seasonal-pick: initial schema
--
-- Apply with:
--   createdb seasonal_pick
--   psql -d seasonal_pick -f collector/sql/001_schema.sql
--
-- Numbered files are applied in ascending order. A file is never edited once
-- it has been applied; later changes go into a new numbered file.
--
-- Every code column is text. Leading zeros carry meaning in the aT code
-- system: grade "01" (1등급) and "1" (1++등급) are different grades, so
-- parsing codes as integers would merge distinct rows.

BEGIN;

-- ============================================================
-- Master tables. Seeded from collector/seed/codes/*.csv in 002.
-- ============================================================

-- 01 retail, 02 wholesale, 03 and 07 eco-friendly produce.
CREATE TABLE trade_types (
    se_cd text PRIMARY KEY,
    se_nm text NOT NULL
);

CREATE TABLE categories (
    ctgry_cd text PRIMARY KEY,
    ctgry_nm text NOT NULL
);

-- item_cd is 3 or 4 characters depending on the category, so nothing may
-- assume a fixed width.
CREATE TABLE items (
    ctgry_cd text NOT NULL REFERENCES categories (ctgry_cd),
    item_cd  text NOT NULL,
    item_nm  text NOT NULL,
    PRIMARY KEY (ctgry_cd, item_cd)
);

-- vrty_cd means different things per category: package size for rice,
-- harvest season for cabbage, country of origin for beans.
CREATE TABLE varieties (
    ctgry_cd text NOT NULL,
    item_cd  text NOT NULL,
    vrty_cd  text NOT NULL,
    vrty_nm  text NOT NULL,
    PRIMARY KEY (ctgry_cd, item_cd, vrty_cd),
    FOREIGN KEY (ctgry_cd, item_cd) REFERENCES items (ctgry_cd, item_cd)
);

-- A grade code is only meaningful inside one (category, item, variety).
-- Code "1" alone stands for a dozen different things across the table.
CREATE TABLE grades (
    ctgry_cd text NOT NULL,
    item_cd  text NOT NULL,
    vrty_cd  text NOT NULL,
    grd_cd   text NOT NULL,
    grd_nm   text NOT NULL,
    PRIMARY KEY (ctgry_cd, item_cd, vrty_cd, grd_cd),
    FOREIGN KEY (ctgry_cd, item_cd, vrty_cd)
        REFERENCES varieties (ctgry_cd, item_cd, vrty_cd)
);

-- The source code table mixes administrative levels: 3100 (Gyeonggi, a
-- province) sits next to 3111 (Suwon, a city inside it), so adding the two
-- together double-counts. `level` records which is which.
--
-- It is nullable here because the source CSV has only two columns and is
-- loaded verbatim; 002 fills it and then fails if any row is left unset.
-- A default such as 'city' is deliberately avoided: a region added to the
-- code table later would silently take a classification nobody checked.
CREATE TABLE regions (
    sgg_cd text PRIMARY KEY,
    sgg_nm text NOT NULL,
    level  text CHECK (level IN ('metro', 'province', 'city', 'special'))
);

-- ============================================================
-- Collection targets
-- ============================================================

-- What the collector fetches and the UI offers. Adding an item is a row
-- here, not a code change.
--
-- Grade and trade type belong to the target, not to the project: "04"
-- (상품) does not exist for chicken or laver, and an item whose grades are
-- all eco-friendly has no rows under retail at all.
CREATE TABLE tracked_targets (
    id         serial PRIMARY KEY,
    ctgry_cd   text NOT NULL,
    item_cd    text NOT NULL,
    vrty_cd    text NOT NULL,
    grd_cd     text NOT NULL,
    se_cd      text NOT NULL REFERENCES trade_types (se_cd),
    display_nm text NOT NULL,
    is_active  boolean NOT NULL DEFAULT true,
    UNIQUE (ctgry_cd, item_cd, vrty_cd, grd_cd, se_cd),
    FOREIGN KEY (ctgry_cd, item_cd, vrty_cd, grd_cd)
        REFERENCES grades (ctgry_cd, item_cd, vrty_cd, grd_cd)
);

-- ============================================================
-- Prices
--
-- Both tables are already aggregated by region upstream: neither carries a
-- market column, so a change in which markets were surveyed cannot move an
-- average and fake a trend.
--
-- Neither references the master tables. aT can publish a code before our
-- code tables know about it, and a foreign key would reject that row and
-- lose the observation. Mismatches are reported by a query instead.
-- ============================================================

-- Source: 지역별 품목별 도·소매가격정보 조회 (daily, per region)
CREATE TABLE price_daily (
    exmn_ymd     date NOT NULL,
    se_cd        text NOT NULL,
    ctgry_cd     text NOT NULL,
    item_cd      text NOT NULL,
    vrty_cd      text NOT NULL,
    grd_cd       text NOT NULL,
    sgg_cd       text NOT NULL,
    unit         text NOT NULL,
    unit_sz      numeric NOT NULL,
    min_prc      integer,
    avg_prc      integer,
    max_prc      integer,
    cnvs_min_prc integer,
    cnvs_avg_prc integer,
    cnvs_max_prc integer,
    collected_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (exmn_ymd, se_cd, ctgry_cd, item_cd, vrty_cd, grd_cd, sgg_cd)
);

-- There is no monthly table. Monthly figures are derived from the rows
-- above: aT's own monthly average equals the simple mean of the daily
-- averages, its monthly high and low are their max and min, and its monthly
-- standard deviation matches the population standard deviation of them once
-- rounded. Keeping a second table would mean storing the same fact twice,
-- and the two would eventually disagree.

-- The primary key leads with the period, which suits a date range but not
-- the main query: one target in one region over time.
CREATE INDEX ix_price_daily_target
    ON price_daily (ctgry_cd, item_cd, vrty_cd, grd_cd, se_cd, sgg_cd, exmn_ymd);

-- ============================================================
-- Collection bookkeeping
-- ============================================================

-- One row per run of one (target, region, period): what was asked for, what
-- came back, and how many calls it cost. API usage has to stay visible
-- because the daily quota cannot be queried, and exceeding it would
-- otherwise fail a run silently.
--
-- 'ok'      the call succeeded. row_count may be 0 - that is a fact, not a
--           diagnosis. Why a period is empty (weekend, holiday, the variety
--           being out of season) is derived from price_daily afterwards: a
--           date that other targets have is a date that was surveyed. The
--           collector does not guess at call time.
-- 'failed'  call error or quota exhausted
--
-- `job` separates the one-off historical backfill from the nightly re-sync.
-- It used to name the API; there is only one now.
CREATE TABLE collection_log (
    id          serial PRIMARY KEY,
    job         text NOT NULL CHECK (job IN ('backfill', 'resync')),
    target_id   integer REFERENCES tracked_targets (id),
    sgg_cd      text,
    period_from date NOT NULL,
    period_to   date NOT NULL,
    status      text NOT NULL CHECK (status IN ('ok', 'failed')),
    row_count   integer NOT NULL DEFAULT 0,
    api_calls   integer NOT NULL DEFAULT 0,
    message     text,
    ran_at      timestamptz NOT NULL DEFAULT now(),
    CHECK (period_from <= period_to)
);

CREATE INDEX ix_collection_log_lookup
    ON collection_log (job, target_id, period_from, period_to);

COMMIT;
