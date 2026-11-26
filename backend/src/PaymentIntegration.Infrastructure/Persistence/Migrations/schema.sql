CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;
DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261125053234_InitialCreate') THEN

    ALTER DATABASE CHARACTER SET utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261125053234_InitialCreate') THEN

    CREATE TABLE `customer` (
        `id` int NOT NULL AUTO_INCREMENT,
        `first_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
        `last_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
        `email` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        CONSTRAINT `PK_customer` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261125053234_InitialCreate') THEN

    CREATE TABLE `product` (
        `id` int NOT NULL AUTO_INCREMENT,
        `name` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
        `description` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
        `price` DECIMAL(12,2) NOT NULL,
        `currency` char(3) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'SGD',
        `payment_type` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
        CONSTRAINT `PK_product` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261125053234_InitialCreate') THEN

    CREATE TABLE `customer_card` (
        `id` int NOT NULL AUTO_INCREMENT,
        `customer_id` int NOT NULL,
        `customer_token` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
        `masked_card_no` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
        `created_at` DATETIME NOT NULL,
        CONSTRAINT `PK_customer_card` PRIMARY KEY (`id`),
        CONSTRAINT `FK_customer_card_customer_customer_id` FOREIGN KEY (`customer_id`) REFERENCES `customer` (`id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261125053234_InitialCreate') THEN

    CREATE TABLE `transaction` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `customer_id` int NOT NULL,
        `product_id` int NOT NULL,
        `invoice_no` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
        `idempotency_key` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
        `amount` DECIMAL(12,2) NOT NULL,
        `currency` char(3) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'SGD',
        `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
        `payment_channel` varchar(20) CHARACTER SET utf8mb4 NULL,
        `masked_card_no` varchar(30) CHARACTER SET utf8mb4 NULL,
        `provider_ref` varchar(50) CHARACTER SET utf8mb4 NULL,
        `resp_code` varchar(10) CHARACTER SET utf8mb4 NULL,
        `resp_desc` varchar(100) CHARACTER SET utf8mb4 NULL,
        `redirect_url` varchar(1000) CHARACTER SET utf8mb4 NULL,
        `created_at` DATETIME NOT NULL,
        `paid_at` DATETIME NULL,
        CONSTRAINT `PK_transaction` PRIMARY KEY (`id`),
        CONSTRAINT `FK_transaction_customer_customer_id` FOREIGN KEY (`customer_id`) REFERENCES `customer` (`id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_transaction_product_product_id` FOREIGN KEY (`product_id`) REFERENCES `product` (`id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261125053234_InitialCreate') THEN

    CREATE UNIQUE INDEX `ix_customer_card_customer_id_token` ON `customer_card` (`customer_id`, `customer_token`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261125053234_InitialCreate') THEN

    CREATE INDEX `IX_transaction_customer_id` ON `transaction` (`customer_id`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261125053234_InitialCreate') THEN

    CREATE UNIQUE INDEX `ix_transaction_idempotency_key` ON `transaction` (`idempotency_key`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261125053234_InitialCreate') THEN

    CREATE UNIQUE INDEX `ix_transaction_invoice_no` ON `transaction` (`invoice_no`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261125053234_InitialCreate') THEN

    CREATE INDEX `IX_transaction_product_id` ON `transaction` (`product_id`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261125053234_InitialCreate') THEN

    CREATE INDEX `ix_transaction_status` ON `transaction` (`status`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20261125053234_InitialCreate') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20261125053234_InitialCreate', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

COMMIT;

