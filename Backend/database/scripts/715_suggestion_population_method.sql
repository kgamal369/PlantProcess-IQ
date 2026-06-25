-- V2-14: persist population (sample size) and analysis method on suggestions so the card can
-- surface "population, method, evidence handle and a euro range". Idempotent + additive.
ALTER TABLE canon.suggestion ADD COLUMN IF NOT EXISTS population integer NOT NULL DEFAULT 0;
ALTER TABLE canon.suggestion ADD COLUMN IF NOT EXISTS method text NOT NULL DEFAULT '';
