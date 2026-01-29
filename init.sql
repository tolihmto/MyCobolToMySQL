-- 1) Base de données
CREATE DATABASE IF NOT EXISTS cobol_studio
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_0900_ai_ci;

-- 2) Utilisateur local Thomas + mot de passe
-- (crée pour localhost. Si tu utilises Docker/WSL, ajoute aussi l’host '%')
CREATE USER IF NOT EXISTS 'username'@'localhost' IDENTIFIED BY 'your_password_1';
-- Optionnel pour accès hors localhost (Docker, autres hôtes)
CREATE USER IF NOT EXISTS 'username'@'%' IDENTIFIED BY 'your_password_2';

-- 3) Droits sur la base
GRANT ALL PRIVILEGES ON cobol_studio.* TO 'username'@'localhost';
GRANT ALL PRIVILEGES ON cobol_studio.* TO 'username'@'%';
FLUSH PRIVILEGES;

-- 4) Sélectionner la base
USE cobol_studio;

-- 5) Table de log d’import (générique)
CREATE TABLE IF NOT EXISTS import_log (
  Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  FileName VARCHAR(255) NOT NULL,
  TableName VARCHAR(255) NOT NULL,
  RowsInserted INT NOT NULL DEFAULT 0,
  Errors INT NOT NULL DEFAULT 0,
  StartedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FinishedAt TIMESTAMP NULL,
  Message TEXT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- 6) Table de staging exemple (alignée avec Samples/sample.cpy)
-- Copybook rappel (simplifié) :
-- 01 CUSTOMER-RECORD.
--    05 CUST-ID           PIC 9(9).
--    05 CUST-NAME         PIC X(20).
--    05 BIRTH-YYYYMMDD    PIC 9(8).
--    05 CONTACT-INFO.
--       10 PHONE-NUMBER   PIC X(12).
--       10 EMAIL          PIC X(25).
--    05 BALANCE           PIC S9(9)V9(2) COMP-3.

CREATE TABLE IF NOT EXISTS staging_customer (
  Id BIGINT NOT NULL AUTO_INCREMENT,
  `CUST_ID` BIGINT NULL,
  `CUST_NAME` VARCHAR(20) NULL,
  `BIRTH_YYYYMMDD` VARCHAR(8) NULL,
  `PHONE_NUMBER` VARCHAR(12) NULL,
  `EMAIL` VARCHAR(255) NULL,
  `BALANCE` DECIMAL(12,2) NULL,  -- S9(9)V9(2) => DECIMAL(12,2) (approximation)
  `ImportFileName` VARCHAR(255) NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- 7) Vue curated de démonstration (utilisée par le bouton "Preview Results")
-- Suppose que tu as importé des données dans staging_customer
CREATE OR REPLACE VIEW curated_view AS
SELECT
  s.*,
  -- Exemple de dérivations (simples) :
  STR_TO_DATE(s.`BIRTH_YYYYMMDD`, '%Y%m%d') AS `BIRTHDATE`,
  -- Placeholder de normalisation (selon règles COBOL, à peaufiner) :
  s.`BALANCE` AS `BALANCE_DEC`
FROM staging_customer s;

USE cobol_studio;
ALTER TABLE staging_customer
  ADD COLUMN `FLAG_A` VARCHAR(1) NULL,
  ADD COLUMN `FLAG_B` VARCHAR(1) NULL;
