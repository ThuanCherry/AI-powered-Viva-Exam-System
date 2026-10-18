-- MySQL dump 10.13  Distrib 8.0.19, for Win64 (x86_64)
--
-- Host: myteam-prn202.c.aivencloud.com    Database: defaultdb
-- ------------------------------------------------------
-- Server version	8.4.8
CREATE DATABASE aives;
USE aives;
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
SET @MYSQLDUMP_TEMP_LOG_BIN = @@SESSION.SQL_LOG_BIN;
SET @@SESSION.SQL_LOG_BIN= 0;

--
-- GTID state at the beginning of the backup 
--

SET @@GLOBAL.GTID_PURGED=/*!80000 '+'*/ '98e16368-bb59-11f1-ad39-1a2eafa94605:1-389,
dc75ba5f-ba85-11f1-b799-32efa41dd4b2:1-89';

--
-- Table structure for table `academic_classes`
--

DROP TABLE IF EXISTS `academic_classes`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `academic_classes` (
  `class_id` int NOT NULL AUTO_INCREMENT,
  `class_code` varchar(50) NOT NULL,
  `class_name` varchar(200) NOT NULL,
  `course_id` int NOT NULL,
  `lecturer_id` int NOT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT '1',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT NULL,
  PRIMARY KEY (`class_id`),
  UNIQUE KEY `uq_academic_classes_code` (`class_code`),
  KEY `ix_academic_classes_course` (`course_id`),
  KEY `ix_academic_classes_lecturer` (`lecturer_id`),
  CONSTRAINT `fk_academic_classes_course` FOREIGN KEY (`course_id`) REFERENCES `courses` (`course_id`),
  CONSTRAINT `fk_academic_classes_lecturer` FOREIGN KEY (`lecturer_id`) REFERENCES `users` (`user_id`)
) ENGINE=InnoDB AUTO_INCREMENT=13 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `ai_evaluations`
--

DROP TABLE IF EXISTS `ai_evaluations`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `ai_evaluations` (
  `evaluation_id` int NOT NULL AUTO_INCREMENT,
  `answer_id` int NOT NULL,
  `ai_score` decimal(5,2) NOT NULL,
  `confidence` decimal(5,4) DEFAULT NULL,
  `feedback` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `strengths` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `weaknesses` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `missing_points` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `evaluated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`evaluation_id`),
  UNIQUE KEY `answer_id` (`answer_id`),
  CONSTRAINT `fk_ai_evaluation_answer` FOREIGN KEY (`answer_id`) REFERENCES `answers` (`answer_id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `answers`
--

DROP TABLE IF EXISTS `answers`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `answers` (
  `answer_id` int NOT NULL AUTO_INCREMENT,
  `exam_question_id` int NOT NULL,
  `candidate_id` int NOT NULL,
  `selected_option_id` int DEFAULT NULL,
  `transcript` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `audio_path` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `started_at` datetime DEFAULT NULL,
  `finished_at` datetime DEFAULT NULL,
  `duration_seconds` int DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`answer_id`),
  UNIQUE KEY `ux_answers_exam_question` (`exam_question_id`),
  KEY `idx_answers_exam_question` (`exam_question_id`),
  KEY `idx_answers_candidate` (`candidate_id`),
  KEY `fk_answers_selected_option` (`selected_option_id`),
  CONSTRAINT `fk_answers_candidate` FOREIGN KEY (`candidate_id`) REFERENCES `exam_candidates` (`candidate_id`),
  CONSTRAINT `fk_answers_exam_question` FOREIGN KEY (`exam_question_id`) REFERENCES `exam_questions` (`exam_question_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_answers_selected_option` FOREIGN KEY (`selected_option_id`) REFERENCES `question_options` (`option_id`) ON DELETE SET NULL
) ENGINE=InnoDB AUTO_INCREMENT=85 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `class_students`
--

DROP TABLE IF EXISTS `class_students`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `class_students` (
  `class_id` int NOT NULL,
  `student_id` int NOT NULL,
  `joined_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`class_id`,`student_id`),
  KEY `ix_class_students_student` (`student_id`),
  CONSTRAINT `fk_class_students_class` FOREIGN KEY (`class_id`) REFERENCES `academic_classes` (`class_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_class_students_student` FOREIGN KEY (`student_id`) REFERENCES `users` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `course_materials`
--

DROP TABLE IF EXISTS `course_materials`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `course_materials` (
  `material_id` int NOT NULL AUTO_INCREMENT,
  `course_id` int NOT NULL,
  `file_name` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `file_path` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `file_type` enum('PDF','DOCX','PPTX') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `file_size` bigint DEFAULT NULL,
  `uploaded_by` int NOT NULL,
  `processing_status` enum('Pending','Processing','Completed','Failed') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Pending',
  `uploaded_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`material_id`),
  KEY `idx_materials_course` (`course_id`),
  KEY `idx_materials_uploader` (`uploaded_by`),
  CONSTRAINT `fk_materials_course` FOREIGN KEY (`course_id`) REFERENCES `courses` (`course_id`),
  CONSTRAINT `fk_materials_uploader` FOREIGN KEY (`uploaded_by`) REFERENCES `users` (`user_id`)
) ENGINE=InnoDB AUTO_INCREMENT=16 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `courses`
--

DROP TABLE IF EXISTS `courses`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `courses` (
  `course_id` int NOT NULL AUTO_INCREMENT,
  `course_code` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `course_name` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `lecturer_id` int NOT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT '1',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`course_id`),
  UNIQUE KEY `course_code` (`course_code`),
  KEY `idx_courses_lecturer` (`lecturer_id`),
  CONSTRAINT `fk_courses_lecturer` FOREIGN KEY (`lecturer_id`) REFERENCES `users` (`user_id`)
) ENGINE=InnoDB AUTO_INCREMENT=15 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `document_chunks`
--

DROP TABLE IF EXISTS `document_chunks`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `document_chunks` (
  `chunk_id` int NOT NULL AUTO_INCREMENT,
  `document_id` int NOT NULL,
  `chunk_index` int NOT NULL,
  `content` longtext COLLATE utf8mb4_unicode_ci NOT NULL,
  `token_count` int NOT NULL,
  `embedding_json` json NOT NULL,
  PRIMARY KEY (`chunk_id`),
  KEY `IX_document_chunks_document_id` (`document_id`),
  CONSTRAINT `FK_document_chunks_documents` FOREIGN KEY (`document_id`) REFERENCES `documents` (`document_id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `documents`
--

DROP TABLE IF EXISTS `documents`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `documents` (
  `document_id` int NOT NULL AUTO_INCREMENT,
  `course_id` int NOT NULL,
  `uploaded_by_id` int NOT NULL,
  `file_name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `content_type` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `file_size` bigint NOT NULL,
  `storage_path` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
  `status` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL,
  `error_message` varchar(1000) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `created_at` datetime(6) NOT NULL,
  `processed_at` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`document_id`),
  KEY `IX_documents_course_id` (`course_id`),
  KEY `IX_documents_uploaded_by_id` (`uploaded_by_id`),
  KEY `IX_documents_status` (`status`),
  CONSTRAINT `FK_documents_courses` FOREIGN KEY (`course_id`) REFERENCES `courses` (`course_id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_documents_users` FOREIGN KEY (`uploaded_by_id`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `exam_candidates`
--

DROP TABLE IF EXISTS `exam_candidates`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `exam_candidates` (
  `candidate_id` int NOT NULL AUTO_INCREMENT,
  `exam_id` int NOT NULL,
  `student_id` int NOT NULL,
  `scheduled_time` datetime DEFAULT NULL,
  `status` enum('Waiting','InProgress','Completed','Absent','Cancelled') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Waiting',
  `started_at` datetime DEFAULT NULL,
  `finished_at` datetime DEFAULT NULL,
  PRIMARY KEY (`candidate_id`),
  UNIQUE KEY `uq_exam_student` (`exam_id`,`student_id`),
  KEY `idx_candidates_exam` (`exam_id`),
  KEY `idx_candidates_student` (`student_id`),
  KEY `idx_candidates_status` (`status`),
  CONSTRAINT `fk_candidates_exam` FOREIGN KEY (`exam_id`) REFERENCES `exam_sessions` (`exam_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_candidates_student` FOREIGN KEY (`student_id`) REFERENCES `users` (`user_id`)
) ENGINE=InnoDB AUTO_INCREMENT=88 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `exam_logs`
--

DROP TABLE IF EXISTS `exam_logs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `exam_logs` (
  `log_id` bigint NOT NULL AUTO_INCREMENT,
  `exam_id` int NOT NULL,
  `candidate_id` int DEFAULT NULL,
  `user_id` int DEFAULT NULL,
  `action` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `timestamp` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`log_id`),
  KEY `idx_logs_exam` (`exam_id`),
  KEY `idx_logs_candidate` (`candidate_id`),
  KEY `idx_logs_user` (`user_id`),
  KEY `idx_logs_timestamp` (`timestamp`),
  CONSTRAINT `fk_logs_candidate` FOREIGN KEY (`candidate_id`) REFERENCES `exam_candidates` (`candidate_id`) ON DELETE SET NULL,
  CONSTRAINT `fk_logs_exam` FOREIGN KEY (`exam_id`) REFERENCES `exam_sessions` (`exam_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_logs_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `exam_questions`
--

DROP TABLE IF EXISTS `exam_questions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `exam_questions` (
  `exam_question_id` int NOT NULL AUTO_INCREMENT,
  `exam_id` int NOT NULL,
  `candidate_id` int NOT NULL,
  `question_id` int NOT NULL,
  `parent_exam_question_id` int DEFAULT NULL,
  `order_no` int NOT NULL,
  `asked_at` datetime DEFAULT NULL,
  `is_completed` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`exam_question_id`),
  KEY `idx_exam_questions_exam` (`exam_id`),
  KEY `idx_exam_questions_candidate` (`candidate_id`),
  KEY `idx_exam_questions_question` (`question_id`),
  KEY `idx_candidate_order` (`candidate_id`,`order_no`),
  KEY `fk_exam_questions_parent` (`parent_exam_question_id`),
  CONSTRAINT `fk_exam_questions_candidate` FOREIGN KEY (`candidate_id`) REFERENCES `exam_candidates` (`candidate_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_exam_questions_exam` FOREIGN KEY (`exam_id`) REFERENCES `exam_sessions` (`exam_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_exam_questions_parent` FOREIGN KEY (`parent_exam_question_id`) REFERENCES `exam_questions` (`exam_question_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_exam_questions_question` FOREIGN KEY (`question_id`) REFERENCES `questions` (`question_id`)
) ENGINE=InnoDB AUTO_INCREMENT=103 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `exam_recordings`
--

DROP TABLE IF EXISTS `exam_recordings`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `exam_recordings` (
  `recording_id` int NOT NULL AUTO_INCREMENT,
  `exam_id` int NOT NULL,
  `candidate_id` int NOT NULL,
  `file_path` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `file_type` enum('Audio','Video') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `duration_seconds` int DEFAULT NULL,
  `started_at` datetime DEFAULT NULL,
  `ended_at` datetime DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`recording_id`),
  KEY `idx_recording_exam` (`exam_id`),
  KEY `idx_recording_candidate` (`candidate_id`),
  CONSTRAINT `fk_recording_candidate` FOREIGN KEY (`candidate_id`) REFERENCES `exam_candidates` (`candidate_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_recording_exam` FOREIGN KEY (`exam_id`) REFERENCES `exam_sessions` (`exam_id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `exam_sessions`
--

DROP TABLE IF EXISTS `exam_sessions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `exam_sessions` (
  `exam_id` int NOT NULL AUTO_INCREMENT,
  `course_id` int NOT NULL,
  `lecturer_id` int NOT NULL,
  `exam_name` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `start_time` datetime NOT NULL,
  `end_time` datetime NOT NULL,
  `time_per_student` int NOT NULL,
  `main_question_count` int NOT NULL DEFAULT '3',
  `max_follow_up_count` int NOT NULL DEFAULT '2',
  `status` enum('Draft','Scheduled','Ready','InProgress','Completed','Cancelled') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Draft',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `question_scope_json` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`exam_id`),
  KEY `idx_exam_course` (`course_id`),
  KEY `idx_exam_lecturer` (`lecturer_id`),
  KEY `idx_exam_status` (`status`),
  CONSTRAINT `fk_exam_course` FOREIGN KEY (`course_id`) REFERENCES `courses` (`course_id`),
  CONSTRAINT `fk_exam_lecturer` FOREIGN KEY (`lecturer_id`) REFERENCES `users` (`user_id`)
) ENGINE=InnoDB AUTO_INCREMENT=41 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `exam_viva_settings`
--

DROP TABLE IF EXISTS `exam_viva_settings`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `exam_viva_settings` (
  `exam_id` int NOT NULL,
  `seconds_per_question` int NOT NULL DEFAULT '120',
  `max_follow_ups_per_question` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`exam_id`),
  CONSTRAINT `fk_viva_settings_exam` FOREIGN KEY (`exam_id`) REFERENCES `exam_sessions` (`exam_id`) ON DELETE CASCADE,
  CONSTRAINT `ck_viva_followups` CHECK ((`max_follow_ups_per_question` between 0 and 5)),
  CONSTRAINT `ck_viva_seconds` CHECK ((`seconds_per_question` between 15 and 1800))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `final_results`
--

DROP TABLE IF EXISTS `final_results`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `final_results` (
  `result_id` int NOT NULL AUTO_INCREMENT,
  `exam_id` int NOT NULL,
  `candidate_id` int NOT NULL,
  `ai_score` decimal(5,2) DEFAULT NULL,
  `lecturer_score` decimal(5,2) DEFAULT NULL,
  `final_score` decimal(5,2) DEFAULT NULL,
  `lecturer_comment` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `finalized_by` int DEFAULT NULL,
  `finalized_at` datetime DEFAULT NULL,
  PRIMARY KEY (`result_id`),
  UNIQUE KEY `uq_result_exam_candidate` (`exam_id`,`candidate_id`),
  KEY `idx_results_exam` (`exam_id`),
  KEY `idx_results_candidate` (`candidate_id`),
  KEY `idx_results_finalized_by` (`finalized_by`),
  CONSTRAINT `fk_result_candidate` FOREIGN KEY (`candidate_id`) REFERENCES `exam_candidates` (`candidate_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_result_exam` FOREIGN KEY (`exam_id`) REFERENCES `exam_sessions` (`exam_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_result_lecturer` FOREIGN KEY (`finalized_by`) REFERENCES `users` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `follow_up_questions`
--

DROP TABLE IF EXISTS `follow_up_questions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `follow_up_questions` (
  `follow_up_id` int NOT NULL AUTO_INCREMENT,
  `answer_id` int NOT NULL,
  `question_text` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `answer_transcript` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `audio_path` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `reason` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `order_no` int NOT NULL DEFAULT '1',
  `asked_at` datetime DEFAULT NULL,
  `answered_at` datetime DEFAULT NULL,
  PRIMARY KEY (`follow_up_id`),
  KEY `idx_followup_answer` (`answer_id`),
  CONSTRAINT `fk_followup_answer` FOREIGN KEY (`answer_id`) REFERENCES `answers` (`answer_id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `generated_questions`
--

DROP TABLE IF EXISTS `generated_questions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `generated_questions` (
  `question_id` int NOT NULL AUTO_INCREMENT,
  `document_id` int NOT NULL,
  `course_id` int DEFAULT NULL,
  `question_text` longtext COLLATE utf8mb4_unicode_ci NOT NULL,
  `answer_key` varchar(2000) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `explanation` varchar(2000) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `bloom_level` int NOT NULL,
  `topic` varchar(200) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `status` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL,
  `reviewed_by_id` int DEFAULT NULL,
  `reviewed_at` datetime(6) DEFAULT NULL,
  `review_notes` varchar(1000) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `created_at` datetime(6) NOT NULL,
  PRIMARY KEY (`question_id`),
  KEY `IX_generated_questions_document_id` (`document_id`),
  KEY `IX_generated_questions_course_id` (`course_id`),
  KEY `IX_generated_questions_reviewed_by_id` (`reviewed_by_id`),
  KEY `IX_generated_questions_status` (`status`),
  CONSTRAINT `FK_generated_questions_courses` FOREIGN KEY (`course_id`) REFERENCES `courses` (`course_id`) ON DELETE SET NULL,
  CONSTRAINT `FK_generated_questions_documents` FOREIGN KEY (`document_id`) REFERENCES `documents` (`document_id`) ON DELETE CASCADE,
  CONSTRAINT `FK_generated_questions_users` FOREIGN KEY (`reviewed_by_id`) REFERENCES `users` (`user_id`) ON DELETE SET NULL
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `notifications`
--

DROP TABLE IF EXISTS `notifications`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `notifications` (
  `notification_id` int NOT NULL AUTO_INCREMENT,
  `user_id` int NOT NULL,
  `course_id` int DEFAULT NULL,
  `exam_id` int DEFAULT NULL,
  `type` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `message` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `is_read` tinyint(1) NOT NULL DEFAULT '0',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`notification_id`),
  KEY `idx_notifications_user_read` (`user_id`,`is_read`),
  KEY `idx_notifications_course` (`course_id`),
  KEY `idx_notifications_exam` (`exam_id`),
  CONSTRAINT `fk_notifications_course` FOREIGN KEY (`course_id`) REFERENCES `courses` (`course_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_notifications_exam` FOREIGN KEY (`exam_id`) REFERENCES `exam_sessions` (`exam_id`) ON DELETE CASCADE,
  CONSTRAINT `fk_notifications_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `question_options`
--

DROP TABLE IF EXISTS `question_options`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `question_options` (
  `option_id` int NOT NULL AUTO_INCREMENT,
  `question_id` int NOT NULL,
  `option_text` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `is_correct` tinyint(1) NOT NULL DEFAULT '0',
  `display_order` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`option_id`),
  KEY `ix_question_options_question` (`question_id`),
  CONSTRAINT `fk_question_options_question` FOREIGN KEY (`question_id`) REFERENCES `questions` (`question_id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=451 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `questions`
--

DROP TABLE IF EXISTS `questions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `questions` (
  `question_id` int NOT NULL AUTO_INCREMENT,
  `course_id` int NOT NULL,
  `source_material_id` int DEFAULT NULL,
  `created_by` int NOT NULL,
  `question_text` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `expected_answer` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `bloom_level` enum('Remember','Understand','Apply','Analyze') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `difficulty` enum('Easy','Medium','Hard') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `question_type` enum('Main','FollowUp') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Main',
  `status` enum('Draft','PendingReview','Approved','Rejected','Archived') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Draft',
  `source` enum('Manual','AI') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Manual',
  `reviewed_by` int DEFAULT NULL,
  `reviewed_at` datetime DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`question_id`),
  KEY `idx_questions_course` (`course_id`),
  KEY `idx_questions_material` (`source_material_id`),
  KEY `idx_questions_creator` (`created_by`),
  KEY `idx_questions_status` (`status`),
  KEY `idx_questions_bloom` (`bloom_level`),
  KEY `idx_questions_bank` (`course_id`,`question_type`,`status`),
  KEY `idx_questions_source` (`source`),
  KEY `idx_questions_reviewer` (`reviewed_by`),
  CONSTRAINT `fk_questions_course` FOREIGN KEY (`course_id`) REFERENCES `courses` (`course_id`),
  CONSTRAINT `fk_questions_creator` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`),
  CONSTRAINT `fk_questions_material` FOREIGN KEY (`source_material_id`) REFERENCES `course_materials` (`material_id`) ON DELETE SET NULL,
  CONSTRAINT `fk_questions_reviewer` FOREIGN KEY (`reviewed_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL
) ENGINE=InnoDB AUTO_INCREMENT=112 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `roles`
--

DROP TABLE IF EXISTS `roles`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `roles` (
  `role_id` int NOT NULL AUTO_INCREMENT,
  `role_name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`role_id`),
  UNIQUE KEY `role_name` (`role_name`)
) ENGINE=InnoDB AUTO_INCREMENT=4 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `rubric_criteria`
--

DROP TABLE IF EXISTS `rubric_criteria`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `rubric_criteria` (
  `criteria_id` int NOT NULL AUTO_INCREMENT,
  `rubric_id` int NOT NULL,
  `criteria_name` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `max_score` decimal(5,2) NOT NULL,
  `display_order` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`criteria_id`),
  KEY `idx_criteria_rubric` (`rubric_id`),
  CONSTRAINT `fk_criteria_rubric` FOREIGN KEY (`rubric_id`) REFERENCES `rubrics` (`rubric_id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=11 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `rubrics`
--

DROP TABLE IF EXISTS `rubrics`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `rubrics` (
  `rubric_id` int NOT NULL AUTO_INCREMENT,
  `question_id` int NOT NULL,
  `total_score` decimal(5,2) NOT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`rubric_id`),
  UNIQUE KEY `question_id` (`question_id`),
  CONSTRAINT `fk_rubrics_question` FOREIGN KEY (`question_id`) REFERENCES `questions` (`question_id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=7 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `users`
--

DROP TABLE IF EXISTS `users`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `users` (
  `user_id` int NOT NULL AUTO_INCREMENT,
  `role_id` int NOT NULL,
  `full_name` varchar(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `email` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `password_hash` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `status` enum('Active','Inactive','Locked') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Active',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`user_id`),
  UNIQUE KEY `email` (`email`),
  KEY `idx_users_role` (`role_id`),
  CONSTRAINT `fk_users_role` FOREIGN KEY (`role_id`) REFERENCES `roles` (`role_id`)
) ENGINE=InnoDB AUTO_INCREMENT=30 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping routines for database 'defaultdb'
--
SET @@SESSION.SQL_LOG_BIN = @MYSQLDUMP_TEMP_LOG_BIN;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2026-10-15  7:55:16

--
-- Seed initial roles
--
INSERT INTO `roles` (`role_id`, `role_name`, `description`) VALUES
(1, 'Admin', 'System Administrator'),
(2, 'Lecturer', 'Course Lecturer'),
(3, 'Student', 'Candidate Student')
ON DUPLICATE KEY UPDATE `description` = VALUES(`description`);

--
-- Seed Users (Default password for all accounts: Password123!)
-- Hash: $2a$11$/hlOxt0A9jrv8utlMD.7suzl49dz7kl3KM4qPM/U63fxzNkTLTqIe
--
INSERT INTO `users` (`user_id`, `role_id`, `full_name`, `email`, `password_hash`, `status`, `created_at`) VALUES
(1, 2, 'Dr. Tran Van A', 'lecturer@fpt.edu.vn', '$2a$11$/hlOxt0A9jrv8utlMD.7suzl49dz7kl3KM4qPM/U63fxzNkTLTqIe', 'Active', NOW()),
(2, 1, 'Administrator', 'admin@fpt.edu.vn', '$2a$11$/hlOxt0A9jrv8utlMD.7suzl49dz7kl3KM4qPM/U63fxzNkTLTqIe', 'Active', NOW()),
(3, 3, 'Nguyen Minh Anh', 'anh.nm@fpt.edu.vn', '$2a$11$/hlOxt0A9jrv8utlMD.7suzl49dz7kl3KM4qPM/U63fxzNkTLTqIe', 'Active', NOW()),
(4, 3, 'Pham Gia Han', 'han.pg@fpt.edu.vn', '$2a$11$/hlOxt0A9jrv8utlMD.7suzl49dz7kl3KM4qPM/U63fxzNkTLTqIe', 'Active', NOW()),
(5, 3, 'Le Duc Thinh', 'thinh.ld@fpt.edu.vn', '$2a$11$/hlOxt0A9jrv8utlMD.7suzl49dz7kl3KM4qPM/U63fxzNkTLTqIe', 'Active', NOW()),
(6, 3, 'Vo Thi Kim Ngan', 'ngan.vtk@fpt.edu.vn', '$2a$11$/hlOxt0A9jrv8utlMD.7suzl49dz7kl3KM4qPM/U63fxzNkTLTqIe', 'Active', NOW()),
(7, 3, 'Tran Quoc Bao', 'bao.tq@fpt.edu.vn', '$2a$11$/hlOxt0A9jrv8utlMD.7suzl49dz7kl3KM4qPM/U63fxzNkTLTqIe', 'Active', NOW()),
(8, 3, 'Dang Thu Trang', 'trang.dt@fpt.edu.vn', '$2a$11$/hlOxt0A9jrv8utlMD.7suzl49dz7kl3KM4qPM/U63fxzNkTLTqIe', 'Active', NOW())
ON DUPLICATE KEY UPDATE `full_name` = VALUES(`full_name`), `password_hash` = VALUES(`password_hash`);

--
-- Seed Courses
--
INSERT INTO `courses` (`course_id`, `course_code`, `course_name`, `description`, `lecturer_id`, `is_active`, `created_at`) VALUES
(1, 'SWD392', 'Software Architecture & Design', 'Architectural styles, patterns, microservices, and system quality attributes.', 1, 1, NOW()),
(2, 'DBI202', 'Database Systems', 'Relational data modeling, SQL queries, indexing, and transaction management.', 1, 1, NOW()),
(3, 'PRN231', 'Cross-Platform Web APIs with .NET', 'ASP.NET Core RESTful APIs, Clean Architecture, Entity Framework Core, and SignalR.', 1, 1, NOW())
ON DUPLICATE KEY UPDATE `course_name` = VALUES(`course_name`);

--
-- Seed Academic Classes & Students
--
INSERT INTO `academic_classes` (`class_id`, `class_code`, `class_name`, `course_id`, `lecturer_id`, `is_active`, `created_at`) VALUES
(1, 'SE1801-SWD', 'K18 Software Architecture - Class 01', 1, 1, 1, NOW()),
(2, 'SE1802-DBI', 'K18 Database Systems - Class 02', 2, 1, 1, NOW())
ON DUPLICATE KEY UPDATE `class_name` = VALUES(`class_name`);

INSERT INTO `class_students` (`class_id`, `student_id`, `joined_at`) VALUES
(1, 3, NOW()),
(1, 4, NOW()),
(1, 5, NOW()),
(1, 6, NOW()),
(1, 7, NOW()),
(1, 8, NOW()),
(2, 3, NOW()),
(2, 4, NOW()),
(2, 5, NOW())
ON DUPLICATE KEY UPDATE `joined_at` = VALUES(`joined_at`);

--
-- Seed Question Pool (Questions for SWD392 to test Anti-Overlap Distribution)
--
INSERT INTO `questions` (`question_id`, `course_id`, `source_material_id`, `created_by`, `question_text`, `expected_answer`, `bloom_level`, `difficulty`, `question_type`, `status`, `source`, `created_at`) VALUES
(1, 1, NULL, 1, 'Explain the Single Responsibility Principle (SRP) and provide an example of violating it.', 'Each class should have only one reason to change, meaning it should focus on a single responsibility.', 'Understand', 'Easy', 'Main', 'Approved', 'Manual', NOW()),
(2, 1, NULL, 1, 'Compare Monolithic Architecture with Microservices Architecture. When should a team choose one over the other?', 'Monoliths are easier to deploy and test initially, but Microservices scale independently and allow multiple tech stacks.', 'Analyze', 'Medium', 'Main', 'Approved', 'Manual', NOW()),
(3, 1, NULL, 1, 'What is Dependency Inversion Principle (DIP) and how does it relate to Inversion of Control (IoC) containers?', 'High-level modules should not depend on low-level modules; both should depend on abstractions.', 'Apply', 'Medium', 'Main', 'Approved', 'Manual', NOW()),
(4, 1, NULL, 1, 'Explain how the Circuit Breaker pattern prevents cascading failures in distributed systems.', 'It wraps calls and trips open when failures reach a threshold, returning fallback responses instead of wasting resources.', 'Understand', 'Hard', 'Main', 'Approved', 'Manual', NOW()),
(5, 1, NULL, 1, 'Describe the CQRS (Command Query Responsibility Segregation) pattern and its advantages for high-read applications.', 'Separates read operations from update operations to optimize scaling, caching, and schemas independently.', 'Analyze', 'Hard', 'Main', 'Approved', 'Manual', NOW()),
(6, 1, NULL, 1, 'What is the purpose of an API Gateway in a microservice ecosystem?', 'Handles routing, authentication, rate limiting, and request aggregation for client apps.', 'Understand', 'Easy', 'Main', 'Approved', 'Manual', NOW()),
(7, 1, NULL, 1, 'Describe how Event-Driven Architecture ensures loose coupling between disparate services.', 'Services publish events to message brokers without direct coupling to consumers, enabling asynchronous handling.', 'Apply', 'Medium', 'Main', 'Approved', 'Manual', NOW()),
(8, 1, NULL, 1, 'Explain the differences between Pessimistic Locking and Optimistic Locking with concurrency control.', 'Optimistic assumes collisions are rare and verifies with version tokens; pessimistic locks resources up front.', 'Analyze', 'Medium', 'Main', 'Approved', 'Manual', NOW()),
(9, 1, NULL, 1, 'What are the main tradeoffs when choosing between REST and gRPC for inter-service communication?', 'REST is HTTP/JSON and human-readable; gRPC uses HTTP/2 and Protobuf for high-performance low-latency communication.', 'Analyze', 'Hard', 'Main', 'Approved', 'Manual', NOW()),
(10, 1, NULL, 1, 'Explain the Repository and Unit of Work patterns and how they abstract data access.', 'Repository mediates between domain and data mapping; Unit of Work maintains a list of objects affected by a business transaction.', 'Understand', 'Easy', 'Main', 'Approved', 'Manual', NOW())
ON DUPLICATE KEY UPDATE `question_text` = VALUES(`question_text`);

--
-- Seed Sample Viva Exam Session (Exam 1: Ready status with candidates and anti-overlap questions)
--
INSERT INTO `exam_sessions` (`exam_id`, `course_id`, `lecturer_id`, `exam_name`, `description`, `start_time`, `end_time`, `time_per_student`, `main_question_count`, `max_follow_up_count`, `status`, `created_at`, `question_scope_json`) VALUES
(1, 1, 1, 'Midterm Oral Defense - Software Architecture', 'Viva examination covering SOLID principles, architectural styles, and design patterns.', '2026-10-20 08:00:00', '2026-10-20 17:00:00', 20, 3, 2, 'Ready', NOW(), '{"strategy":"RANDOM","selectedQuestionIds":[1,2,3,4,5,6,7,8,9,10],"totalAssigned":12}')
ON DUPLICATE KEY UPDATE `exam_name` = VALUES(`exam_name`);

INSERT INTO `exam_viva_settings` (`exam_id`, `seconds_per_question`, `max_follow_ups_per_question`) VALUES
(1, 120, 2)
ON DUPLICATE KEY UPDATE `seconds_per_question` = VALUES(`seconds_per_question`);

-- Candidates scheduled for Exam 1
INSERT INTO `exam_candidates` (`candidate_id`, `exam_id`, `student_id`, `scheduled_time`, `status`, `started_at`, `finished_at`) VALUES
(1, 1, 3, '2026-10-20 08:00:00', 'Waiting', NULL, NULL),
(2, 1, 4, '2026-10-20 08:25:00', 'Waiting', NULL, NULL),
(3, 1, 5, '2026-10-20 08:50:00', 'Waiting', NULL, NULL),
(4, 1, 6, '2026-10-20 09:15:00', 'Waiting', NULL, NULL)
ON DUPLICATE KEY UPDATE `scheduled_time` = VALUES(`scheduled_time`);

-- Pre-assigned questions ensuring consecutive candidates don't get the same questions:
-- Candidate 1 (Student 3): Questions 1, 2, 3
-- Candidate 2 (Student 4): Questions 4, 5, 6  (0 overlap with Candidate 1!)
-- Candidate 3 (Student 5): Questions 7, 8, 9  (0 overlap with Candidate 2!)
-- Candidate 4 (Student 6): Questions 1, 10, 2 (0 overlap with Candidate 3!)
INSERT INTO `exam_questions` (`exam_question_id`, `exam_id`, `candidate_id`, `question_id`, `order_no`, `is_completed`) VALUES
(1, 1, 1, 1, 1, 0),
(2, 1, 1, 2, 2, 0),
(3, 1, 1, 3, 3, 0),
(4, 1, 2, 4, 1, 0),
(5, 1, 2, 5, 2, 0),
(6, 1, 2, 6, 3, 0),
(7, 1, 3, 7, 1, 0),
(8, 1, 3, 8, 2, 0),
(9, 1, 3, 9, 3, 0),
(10, 1, 4, 1, 1, 0),
(11, 1, 4, 10, 2, 0),
(12, 1, 4, 2, 3, 0)
ON DUPLICATE KEY UPDATE `question_id` = VALUES(`question_id`);
