-- MySQL dump 10.13  Distrib 8.0.21, for Win64 (x86_64)
--
-- Host: 106.54.9.137    Database: googleai
-- ------------------------------------------------------
-- Server version	8.3.0

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!50503 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Table structure for table `drawinghistory`
--

DROP TABLE IF EXISTS `drawinghistory`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `drawinghistory` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserId` int NOT NULL,
  `TaskId` int NOT NULL,
  `ModelName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `Prompt` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `ImageUrl` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `TaskMode` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `CreatedAt` datetime DEFAULT CURRENT_TIMESTAMP,
  `ThumbnailUrl` text COLLATE utf8mb4_unicode_ci,
  `IsR2Uploaded` tinyint(1) DEFAULT '0' COMMENT '是否已上传到R2存储',
  `UrlVersion` int DEFAULT '0' COMMENT 'URL版本号',
  `OriginalImageUrl` varchar(1000) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '原始API返回的图片URL',
  `LastUpdatedAt` datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`),
  KEY `idx_user_id` (`UserId`),
  KEY `idx_task_id` (`TaskId`),
  KEY `idx_created_at` (`CreatedAt`),
  KEY `idx_history_task_id` (`TaskId`),
  CONSTRAINT `drawinghistory_ibfk_1` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `drawinghistory_ibfk_2` FOREIGN KEY (`TaskId`) REFERENCES `drawingtasks` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=589 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;


--
-- Table structure for table `drawingtasks`
--

DROP TABLE IF EXISTS `drawingtasks`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `drawingtasks` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserId` int NOT NULL,
  `ModelId` int NOT NULL,
  `TaskMode` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'generate, localEdit, fusion, textEdit, consistency',
  `Prompt` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `TaskStatus` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT 'Pending' COMMENT 'Pending, Processing, Completed, Failed',
  `ResultImageUrl` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `ErrorMessage` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `ReferenceImages` json DEFAULT NULL COMMENT 'JSON array of reference image URLs/base64',
  `CreatedAt` datetime DEFAULT CURRENT_TIMESTAMP,
  `CompletedAt` datetime DEFAULT NULL,
  `ThumbnailUrl` text COLLATE utf8mb4_unicode_ci,
  `Progress` int DEFAULT '0' COMMENT '任务进度百分比(0-100)',
  `ProgressMessage` text COLLATE utf8mb4_unicode_ci COMMENT '任务进度描述信息',
  `AspectRatio` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `IsR2Uploaded` tinyint(1) DEFAULT '0' COMMENT '是否已上传到R2存储',
  `UrlVersion` int DEFAULT '0' COMMENT 'URL版本号，用于乐观锁',
  `OriginalImageUrl` varchar(1000) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '原始API返回的图片URL',
  `LastUpdatedAt` datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '最后更新时间',
  `ParentTaskId` int DEFAULT NULL COMMENT '父任务ID（如果是拆分任务）',
  `SplitIndex` int DEFAULT NULL COMMENT '分块索引（在父任务中的位置）',
  `BatchGroupId` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '批次组ID（用于批量任务）',
  `ProcessMode` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `Tolerance` double DEFAULT NULL,
  `ResultImageUrls` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`Id`),
  KEY `idx_user_id` (`UserId`),
  KEY `idx_model_id` (`ModelId`),
  KEY `idx_task_status` (`TaskStatus`),
  KEY `idx_created_at` (`CreatedAt`),
  KEY `idx_drawing_tasks_progress` (`Progress`),
  KEY `idx_drawing_tasks_status_progress` (`TaskStatus`,`Progress`),
  KEY `idx_task_status_created` (`TaskStatus`,`CreatedAt`),
  KEY `idx_parent_task_id` (`ParentTaskId`),
  KEY `idx_batch_group_id` (`BatchGroupId`),
  KEY `idx_batch_split` (`BatchGroupId`,`SplitIndex`),
  CONSTRAINT `drawingtasks_ibfk_1` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `drawingtasks_ibfk_2` FOREIGN KEY (`ModelId`) REFERENCES `modelconfigurations` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB AUTO_INCREMENT=772 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;


--
-- Dumping data for table `modelconfigurations`
--

LOCK TABLES `modelconfigurations` WRITE;
/*!40000 ALTER TABLE `modelconfigurations` DISABLE KEYS */;
INSERT INTO `modelconfigurations` VALUES (1,'Nano-Banana-2','https://api.gptgod.online/v1/chat/completions','sk-xx',1,100000,0.70,'Nano-Banana-Pro-2K-通道4','2025-12-02 15:26:59','2026-01-02 10:12:31',30,'2K','Chat'),(2,'Nano-Banana-2-4k','https://api.gptgod.online/v1/chat/completions','sk-xx',1,100000,0.70,'Nano-Banana-Pro-4K-通道4','2025-12-02 15:25:59','2026-01-02 10:12:31',30,'4K','Chat'),(3,'Nano-Banana-Fast','https://grsai.dakka.com.cn/v1/draw/nano-banana','sk-xx',1,100000,0.70,'Nano-Banana-Fast-通道1','2025-12-02 15:32:49','2026-01-02 10:13:15',10,'1K','Draw'),(4,'Nano-Banana-Pro','https://grsai.dakka.com.cn/v1/draw/nano-banana','sk-xx',1,100000,0.70,'Nano-Banana-Pro-1K-通道1','2025-12-02 15:35:49','2026-01-02 10:13:15',20,'1K','Draw'),(6,'Nano-Banana-Pro-vt','https://grsai.dakka.com.cn/v1/draw/nano-banana','sk-xx',1,100000,0.70,'Nano-Banana-Pro-2K-通道1','2025-12-02 15:35:49','2026-01-02 10:13:15',20,'2K','Draw'),(7,'Nano-Banana-Pro-vt','https://grsai.dakka.com.cn/v1/draw/nano-banana','sk-xx',1,100000,0.70,'Nano-Banana-Pro-4K-通道1','2025-12-02 15:35:49','2026-01-02 10:13:15',20,'4K','Draw'),(8,'nano-banana-pro','https://api.xbyjs.top/v1/chat/completions','sk-xx',1,100000,0.70,'Nano-Banana-Pro-实惠通道-多图','2025-12-05 15:35:49','2026-01-02 10:17:05',5,'2K','MultipleChat'),(9,'nano-banana-pro-4k','https://api.xbyjs.top/v1/chat/completions','sk-xx',1,100000,0.70,'Nano-Banana-Pro-4K-实惠通道-多图','2025-12-02 15:35:49','2026-01-02 10:17:05',10,'4K','MultipleChat'),(10,'Nano-Banana','https://api.xbyjs.top/v1/chat/completions','sk-xx',1,100000,0.70,'Nano-Banana-1K-实惠通道','2025-12-02 15:35:49','2026-01-02 10:24:52',2,'1K','MultipleChat'),(11,'nano-banana-pro-cl','https://grsai.dakka.com.cn/v1/draw/nano-banana','sk-xx',1,100000,0.70,'Nano-Banana-Pro-1K-稳定通道','2025-12-06 15:51:49','2026-01-02 10:26:33',35,'1K','Draw'),(12,'nano-banana-pro-cl','https://grsai.dakka.com.cn/v1/draw/nano-banana','sk-xx',1,100000,0.70,'Nano-Banana-Pro-2K-稳定通道','2025-12-06 15:50:49','2026-01-02 10:26:37',35,'2K','Draw'),(13,'nano-banana-pro-cl','https://grsai.dakka.com.cn/v1/draw/nano-banana','sk-xx',1,100000,0.70,'Nano-Banana-Pro-4K-稳定通道','2025-12-06 15:49:49','2026-01-02 10:26:41',35,'4K','Draw');
/*!40000 ALTER TABLE `modelconfigurations` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `paymentorders`
--

DROP TABLE IF EXISTS `paymentorders`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `paymentorders` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `OrderNo` varchar(64) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '订单号',
  `UserId` int NOT NULL COMMENT '用户ID',
  `PackageId` int DEFAULT NULL COMMENT '积分套餐ID',
  `Points` int NOT NULL COMMENT '购买积分数量',
  `Amount` decimal(10,2) NOT NULL COMMENT '支付金额(元)',
  `OrderStatus` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT 'Pending' COMMENT '订单状态: Pending待支付, Paid已支付, Failed失败, Cancelled已取消, Refunded已退款',
  `PaymentType` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '支付类型: WeChat微信支付, Alipay支付宝',
  `TransactionId` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '第三方交易号',
  `PrepayId` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '微信预支付ID',
  `NotifyTime` datetime DEFAULT NULL COMMENT '支付回调时间',
  `PaidTime` datetime DEFAULT NULL COMMENT '支付完成时间',
  `ErrorMsg` text COLLATE utf8mb4_unicode_ci COMMENT '错误信息',
  `CreatedAt` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdatedAt` datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `OrderNo` (`OrderNo`),
  KEY `idx_order_no` (`OrderNo`),
  KEY `idx_user_id` (`UserId`),
  KEY `idx_order_status` (`OrderStatus`),
  KEY `idx_created_at` (`CreatedAt`),
  KEY `PackageId` (`PackageId`),
  CONSTRAINT `paymentorders_ibfk_1` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `paymentorders_ibfk_2` FOREIGN KEY (`PackageId`) REFERENCES `pointpackages` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='支付订单表';
/*!40101 SET character_set_client = @saved_cs_client */;


--
-- Table structure for table `pointpackages`
--

DROP TABLE IF EXISTS `pointpackages`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `pointpackages` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '套餐名称',
  `Points` int NOT NULL COMMENT '积分数量',
  `Price` decimal(10,2) NOT NULL COMMENT '价格(元)',
  `Description` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '套餐描述',
  `IsActive` tinyint(1) DEFAULT '1' COMMENT '是否启用',
  `SortOrder` int DEFAULT '0' COMMENT '排序序号',
  `CreatedAt` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdatedAt` datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`),
  KEY `idx_is_active` (`IsActive`),
  KEY `idx_sort_order` (`SortOrder`)
) ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='积分套餐表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `pointpackages`
--

LOCK TABLES `pointpackages` WRITE;
/*!40000 ALTER TABLE `pointpackages` DISABLE KEYS */;
INSERT INTO `pointpackages` VALUES (1,'体验套餐',100,1.00,'适合初次体验用户',1,1,'2025-12-27 19:41:07','2025-12-27 19:46:26'),(2,'基础套餐',500,39.90,'适合轻度使用用户',1,2,'2025-12-27 19:41:07','2025-12-27 19:41:07'),(3,'标准套餐',1000,69.90,'性价比之选',1,3,'2025-12-27 19:41:07','2025-12-27 19:41:07'),(4,'高级套餐',2000,129.90,'适合专业用户',1,4,'2025-12-27 19:41:07','2025-12-27 19:41:07'),(5,'尊享套餐',5000,299.90,'超值优惠',1,5,'2025-12-27 19:41:07','2025-12-27 19:41:07');
/*!40000 ALTER TABLE `pointpackages` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `pointshistories`
--

DROP TABLE IF EXISTS `pointshistories`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `pointshistories` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `UserId` bigint NOT NULL COMMENT '用户ID',
  `Points` int NOT NULL COMMENT '积分变动值（正数为增加，负数为减少）',
  `Description` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '积分变动描述',
  `CreatedAt` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`Id`),
  KEY `idx_userid` (`UserId`),
  KEY `idx_createdat` (`CreatedAt`)
) ENGINE=InnoDB AUTO_INCREMENT=47 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='积分历史记录表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `pointshistories`
--

LOCK TABLES `pointshistories` WRITE;
/*!40000 ALTER TABLE `pointshistories` DISABLE KEYS */;
INSERT INTO `pointshistories` VALUES (1,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-08 19:05:30'),(2,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-08 19:07:30'),(3,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-08 19:08:40'),(4,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-08 19:43:18'),(5,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-08 19:44:21'),(6,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-08 21:43:58'),(7,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-08 23:41:08'),(8,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-08 23:42:55'),(9,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-08 23:46:12'),(10,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-08 23:48:04'),(11,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-08 23:50:34'),(12,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-08 23:51:45'),(13,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-09 06:53:35'),(14,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-09 06:59:11'),(15,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-09 08:31:19'),(16,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-09 08:35:08'),(17,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-09 08:36:30'),(18,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-09 09:44:25'),(19,1,0,'使用模型 nano-banana-2-4k 进行绘图','2025-12-09 09:45:58'),(20,1,0,'使用模型 nano-banana-2-4k 进行绘图','2025-12-09 09:47:51'),(21,1,0,'使用模型 nano-banana-2-4k 进行绘图','2025-12-09 11:06:03'),(22,1,0,'使用模型 nano-banana-2-4k 进行绘图','2025-12-09 11:08:30'),(23,1,0,'使用模型 nano-banana-2-4k 进行绘图','2025-12-09 11:14:48'),(24,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-09 12:16:27'),(25,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-09 12:23:00'),(26,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-09 13:02:36'),(27,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-09 13:04:28'),(28,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-09 13:36:03'),(29,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-09 13:48:42'),(30,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-09 13:53:26'),(31,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-09 13:56:34'),(32,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-09 13:58:21'),(33,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-09 13:59:47'),(34,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-09 20:56:41'),(35,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-09 21:02:22'),(36,1,0,'使用模型 nano-banana-2-4k 进行绘图','2025-12-09 21:41:25'),(37,1,0,'使用模型 nano-banana-2-4k 进行绘图','2025-12-09 21:56:12'),(38,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-10 06:54:12'),(39,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-10 06:55:11'),(40,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-10 06:57:02'),(41,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-10 07:01:47'),(42,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-10 07:03:50'),(43,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-10 07:11:03'),(44,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-10 07:26:46'),(45,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-10 07:55:34'),(46,1,0,'使用模型 nano-banana-2 进行绘图','2025-12-10 07:57:45');
/*!40000 ALTER TABLE `pointshistories` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `pointshistory`
--

DROP TABLE IF EXISTS `pointshistory`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `pointshistory` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserId` int NOT NULL,
  `Points` int NOT NULL,
  `Description` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `CreatedAt` datetime NOT NULL,
  `TaskId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `idx_userid` (`UserId`),
  KEY `idx_createdat` (`CreatedAt`)
) ENGINE=InnoDB AUTO_INCREMENT=505 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='积分历史记录表';
/*!40101 SET character_set_client = @saved_cs_client */;


--
-- Table structure for table `qrscansessions`
--

DROP TABLE IF EXISTS `qrscansessions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `qrscansessions` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `SessionId` varchar(64) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '会话ID',
  `Status` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT 'pending' COMMENT '会话状态: pending, scanned, authorized, cancelled, timeout, completed',
  `CreatedAt` datetime DEFAULT CURRENT_TIMESTAMP,
  `ScannedAt` datetime DEFAULT NULL,
  `AuthorizedAt` datetime DEFAULT NULL,
  `ExpiredAt` datetime DEFAULT NULL,
  `OpenId` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `SessionKey` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `NickName` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `AvatarUrl` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `EncryptedData` text COLLATE utf8mb4_unicode_ci,
  `Iv` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `UserId` int DEFAULT NULL,
  `Token` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `SessionId` (`SessionId`),
  KEY `idx_session_id` (`SessionId`),
  KEY `idx_status` (`Status`),
  KEY `idx_expired_at` (`ExpiredAt`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `qrscansessions`
--

LOCK TABLES `qrscansessions` WRITE;
/*!40000 ALTER TABLE `qrscansessions` DISABLE KEYS */;
/*!40000 ALTER TABLE `qrscansessions` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `usercheckins`
--

DROP TABLE IF EXISTS `usercheckins`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `usercheckins` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserId` int NOT NULL,
  `CheckInDate` date NOT NULL,
  `Points` int NOT NULL DEFAULT '0',
  `CreatedAt` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UK_UserId_CheckInDate` (`UserId`,`CheckInDate`),
  KEY `IX_UserCheckIns_CheckInDate` (`CheckInDate`),
  CONSTRAINT `FK_UserCheckIns_Users` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=5 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `usercheckins`
--

LOCK TABLES `usercheckins` WRITE;
/*!40000 ALTER TABLE `usercheckins` DISABLE KEYS */;
INSERT INTO `usercheckins` VALUES (1,7,'2025-12-28',10,'2025-12-28 16:36:29'),(2,1,'2025-12-29',10,'2025-12-29 10:39:39'),(3,1,'2025-12-30',10,'2025-12-30 09:42:39'),(4,7,'2025-12-30',10,'2025-12-30 10:00:24');
/*!40000 ALTER TABLE `usercheckins` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `users`
--

DROP TABLE IF EXISTS `users`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `users` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Username` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `PasswordHash` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `Email` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `CreatedAt` datetime DEFAULT CURRENT_TIMESTAMP,
  `IsActive` tinyint(1) DEFAULT '1',
  `Points` int DEFAULT NULL,
  `CurrentToken` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `IsAdmin` bit(1) DEFAULT NULL,
  `OpenId` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '微信OpenId',
  `UnionId` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '微信UnionId',
  `NickName` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '微信昵称',
  `AvatarUrl` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '微信头像URL',
  `LoginType` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT 'Email' COMMENT '登录类型: Email, WeChat',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `Username` (`Username`),
  KEY `idx_username` (`Username`),
  KEY `idx_openid` (`OpenId`),
  KEY `idx_unionid` (`UnionId`)
) ENGINE=InnoDB AUTO_INCREMENT=12 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `users`
--

LOCK TABLES `users` WRITE;
/*!40000 ALTER TABLE `users` DISABLE KEYS */;
INSERT INTO `users` VALUES (1,'admin','aCS3ipsID8WpKcEgqBNAisfccMilu2T1Kb1rOKeLK+4bm5RtTxwXdk0KFYgZ6lK6','admin@example.com','2025-12-02 15:20:59',1,98431,'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IjEiLCJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1lIjoiYWRtaW4iLCJqdGkiOiIwYzljNGZkZC04ZTk5LTQ4NjEtYjQ1Yy1jNjBjNTgwYmQ3YmIiLCJleHAiOjE3Njc0OTU2ODcsImlzcyI6Ikdvb2dsZUFJIiwiYXVkIjoiR29vZ2xlQUlVc2VycyJ9.6tWASpKDl6f8TZjUk5WQmu4JyMrC6OZgOD-JMm9KWwQ',_binary '\0',NULL,NULL,NULL,NULL,'Email');
/*!40000 ALTER TABLE `users` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Temporary view structure for view `vw_taskstatistics`
--

DROP TABLE IF EXISTS `vw_taskstatistics`;
/*!50001 DROP VIEW IF EXISTS `vw_taskstatistics`*/;
SET @saved_cs_client     = @@character_set_client;
/*!50503 SET character_set_client = utf8mb4 */;
/*!50001 CREATE VIEW `vw_taskstatistics` AS SELECT 
 1 AS `PendingCount`,
 1 AS `ProcessingCount`,
 1 AS `CompletedCount`,
 1 AS `FailedCount`,
 1 AS `PendingR2UploadCount`,
 1 AS `StatisticsTime`*/;
SET character_set_client = @saved_cs_client;

--
-- Table structure for table `wechatloginhistory`
--

DROP TABLE IF EXISTS `wechatloginhistory`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `wechatloginhistory` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserId` int NOT NULL,
  `OpenId` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `UnionId` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `LoginType` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT 'ScanCode' COMMENT '扫码登录或直接登录',
  `LoginTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `IpAddress` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `UserAgent` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `idx_user_id` (`UserId`),
  KEY `idx_openid` (`OpenId`),
  KEY `idx_login_time` (`LoginTime`),
  CONSTRAINT `wechatloginhistory_ibfk_1` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

