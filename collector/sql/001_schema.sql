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

-- Source: 연월별 도·소매가격정보 조회 (monthly and yearly aggregates)
--
-- The dispersion columns are what let a verdict be stated in terms of a
-- normal range rather than a bare percentage: 20% is unremarkable for
-- cabbage and a large move for rice.
CREATE TABLE price_monthly (
    exmn_ym      char(6) NOT NULL CHECK (exmn_ym ~ '^[0-9]{6}$'),
    se_cd        text NOT NULL,
    ctgry_cd     text NOT NULL,
    item_cd      text NOT NULL,
    vrty_cd      text NOT NULL,
    grd_cd       text NOT NULL,
    sgg_cd       text NOT NULL,
    unit         text NOT NULL,
    unit_sz      numeric NOT NULL,
    m_avg_prc    integer,
    m_max_prc    integer,
    m_min_prc    integer,
    m_stddev     numeric,
    m_cv         numeric,
    m_amplitude  numeric,
    y_avg_prc    integer,
    y_max_prc    integer,
    y_min_prc    integer,
    y_stddev     numeric,
    y_cv         numeric,
    y_amplitude  numeric,
    orgnl_reg_dt timestamptz,
    collected_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (exmn_ym, se_cd, ctgry_cd, item_cd, vrty_cd, grd_cd, sgg_cd)
);

-- The primary key leads with the period, which suits a date range but not
-- the main query: one target in one region over time.
CREATE INDEX ix_price_daily_target
    ON price_daily (ctgry_cd, item_cd, vrty_cd, grd_cd, se_cd, sgg_cd, exmn_ymd);

CREATE INDEX ix_price_monthly_target
    ON price_monthly (ctgry_cd, item_cd, vrty_cd, grd_cd, se_cd, sgg_cd, exmn_ym);

-- ============================================================
-- Collection bookkeeping
-- ============================================================

-- Why a period produced no rows, so the collector does not retry a date
-- that was never surveyed, and so API usage stays visible: the daily quota
-- cannot be queried, and exceeding it fails a run silently otherwise.
--
-- 'no_survey'     weekend or public holiday
-- 'out_of_season' the variety is not on the market in that period
-- 'failed'        call error or quota exhausted; retried
CREATE TABLE collection_log (
    id          serial PRIMARY KEY,
    source      text NOT NULL CHECK (source IN ('daily', 'monthly')),
    target_id   integer REFERENCES tracked_targets (id),
    sgg_cd      text,
    period_from date NOT NULL,
    period_to   date NOT NULL,
    status      text NOT NULL
                CHECK (status IN ('ok', 'no_survey', 'out_of_season', 'failed')),
    row_count   integer NOT NULL DEFAULT 0,
    api_calls   integer NOT NULL DEFAULT 0,
    message     text,
    ran_at      timestamptz NOT NULL DEFAULT now(),
    CHECK (period_from <= period_to)
);

CREATE INDEX ix_collection_log_lookup
    ON collection_log (source, target_id, period_from, period_to);

COMMIT;
