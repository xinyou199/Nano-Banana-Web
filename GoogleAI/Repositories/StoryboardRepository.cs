//using Dapper;
//using GoogleAI.Models;
//using MySql.Data.MySqlClient;
//using System.Text.Json;

//namespace GoogleAI.Repositories
//{
//    /// <summary>
//    /// 分镜项目 Repository 接口
//    /// </summary>
//    public interface IStoryboardProjectRepository
//    {
//        // 项目 CRUD
//        Task<int> CreateProjectAsync(StoryboardProject project);
//        Task<StoryboardProject?> GetProjectByIdAsync(int id);
//        Task<IEnumerable<StoryboardProject>> GetUserProjectsAsync(int userId, int pageSize = 20, int offset = 0);
//        Task<bool> UpdateProjectAsync(StoryboardProject project);
//        Task<bool> DeleteProjectAsync(int id);
//        Task<int> GetUserProjectCountAsync(int userId);

//        // 分镜帧 CRUD
//        Task<int> CreateFrameAsync(StoryboardFrame frame);
//        Task<StoryboardFrame?> GetFrameByIdAsync(int id);
//        Task<IEnumerable<StoryboardFrame>> GetProjectFramesAsync(int projectId);
//        Task<bool> UpdateFrameAsync(StoryboardFrame frame);
//        Task<bool> DeleteFrameAsync(int id);
//        Task<bool> DeleteProjectFramesAsync(int projectId);

//        // 查询和统计
//        Task<int> GetProjectFrameCountAsync(int projectId);
//        Task<int> GetProjectCompletedFrameCountAsync(int projectId);
//        Task<StoryboardFrame?> GetFrameByTaskIdAsync(int taskId);
//        Task<IEnumerable<StoryboardFrame>> GetPendingFramesAsync(int limit = 50);

//        // 模板相关
//        Task<int> CreateTemplateAsync(StoryboardTemplate template);
//        Task<StoryboardTemplate?> GetTemplateByShareCodeAsync(string shareCode);
//        Task<bool> UpdateTemplateDownloadCountAsync(string shareCode);
//    }

//    /// <summary>
//    /// 分镜项目 Repository 实现
//    /// </summary>
//    public class StoryboardProjectRepository : IStoryboardProjectRepository
//    {
//        private readonly string _connectionString;

//        public StoryboardProjectRepository(string connectionString)
//        {
//            _connectionString = connectionString;
//        }

//        // ===== 项目 CRUD =====

//        public async Task<int> CreateProjectAsync(StoryboardProject project)
//        {
//            using var connection = new MySqlConnection(_connectionString);
//            var sql = @"
//                INSERT INTO StoryboardProject (
//                    UserId, ProjectName, Description, ModelId, AspectRatio, BasePrompt, 
//                    ReferenceImages, Status, TotalFrames, CompletedFrames, CreatedAt, 
//                    UpdatedAt, IsTemplate, TemplateShareCode, TemplateAuthorId, TemplateDownloads
//                ) VALUES (
//                    @UserId, @ProjectName, @Description, @ModelId, @AspectRatio, @BasePrompt,
//                    @ReferenceImages, @Status, @TotalFrames, @CompletedFrames, @CreatedAt,
//                    @UpdatedAt, @IsTemplate, @TemplateShareCode, @TemplateAuthorId, @TemplateDownloads
//                );
//                SELECT LAST_INSERT_ID();";

//            project.CreatedAt = DateTime.Now;
//            project.UpdatedAt = DateTime.Now;

//            return await connection.ExecuteScalarAsync<int>(sql, project);
//        }

//        public async Task<StoryboardProject?> GetProjectByIdAsync(int id)
//        {
//            using var connection = new MySqlConnection(_connectionString);
//            var sql = "SELECT * FROM StoryboardProject WHERE Id = @Id";
//            return await connection.QueryFirstOrDefaultAsync<StoryboardProject>(sql, new { Id = id });
//        }

//        public async Task<IEnumerable<StoryboardProject>> GetUserProjectsAsync(int userId, int pageSize = 20, int offset = 0)
//        {
//            using var connection = new MySqlConnection(_connectionString);
//            var sql = @"
//                SELECT * FROM StoryboardProject 
//                WHERE UserId = @UserId 
//                ORDER BY UpdatedAt DESC 
//                LIMIT @PageSize OFFSET @Offset";
//            return await connection.QueryAsync<StoryboardProject>(sql, new { UserId = userId, PageSize = pageSize, Offset = offset });
//        }

//        public async Task<bool> UpdateProjectAsync(StoryboardProject project)
//        {
//            using var connection = new MySqlConnection(_connectionString);
//            var sql = @"
//                UPDATE StoryboardProject 
//                SET ProjectName = @ProjectName, 
//                    Description = @Description, 
//                    ModelId = @ModelId,
//                    AspectRatio = @AspectRatio,
//                    BasePrompt = @BasePrompt,
//                    ReferenceImages = @ReferenceImages,
//                    Status = @Status,
//                    ExportData = @ExportData,
//                    TotalFrames = @TotalFrames,
//                    CompletedFrames = @CompletedFrames,
//                    UpdatedAt = @UpdatedAt,
//                    CompletedAt = @CompletedAt,
//                    IsTemplate = @IsTemplate,
//                    TemplateShareCode = @TemplateShareCode,
//                    TemplateAuthorId = @TemplateAuthorId,
//                    TemplateDownloads = @TemplateDownloads
//                WHERE Id = @Id";

//            project.UpdatedAt = DateTime.Now;
//            var result = await connection.ExecuteAsync(sql, project);
//            return result > 0;
//        }

//        public async Task<bool> DeleteProjectAsync(int id)
//        {
//            using var connection = new MySqlConnection(_connectionString);
//            var sql = "DELETE FROM StoryboardProject WHERE Id = @Id";
//            var result = await connection.ExecuteAsync(sql, new { Id = id });
//            return result > 0;
//        }

//        public async Task<int> GetUserProjectCountAsync(int userId)
//        {
//            using var connection = new MySqlConnection(_connectionString);
//            var sql = "SELECT COUNT(*) FROM StoryboardProject WHERE UserId = @UserId";
//            return await connection.ExecuteScalarAsync<int>(sql, new { UserId = userId });
//        }

//        // ===== 分镜帧 CRUD =====

//        public async Task<int> CreateFrameAsync(StoryboardFrame frame)
//        {
//            using var connection = new MySqlConnection(_connectionString);
//            var sql = @"
//                INSERT INTO StoryboardFrame (
//                    ProjectId, FrameIndex, FramePrompt, FrameReferenceImages, TaskId, 
//                    Status, ResultImageUrl, ThumbnailUrl, ErrorMessage, Progress, 
//                    ProgressMessage, CreatedAt, CompletedAt
//                ) VALUES (
//                    @ProjectId, @FrameIndex, @FramePrompt, @FrameReferenceImages, @TaskId,
//                    @Status, @ResultImageUrl, @ThumbnailUrl, @ErrorMessage, @Progress,
//                    @ProgressMessage, @CreatedAt, @CompletedAt
//                );
//                SELECT LAST_INSERT_ID();";

//            frame.CreatedAt = DateTime.Now;
//            return await connection.ExecuteScalarAsync<int>(sql, frame);
//        }

//        public async Task<StoryboardFrame?> GetFrameByIdAsync(int id)
//        {
//            using var connection = new MySqlConnection(_connectionString);
//            var sql = "SELECT * FROM StoryboardFrame WHERE Id = @Id";
//            return await connection.QueryFirstOrDefaultAsync<StoryboardFrame>(sql, new { Id = id });
//        }

//        public async Task<IEnumerable<StoryboardFrame>> GetProjectFramesAsync(int projectId)
//        {
//            using var connection = new MySqlConnection(_connectionString);
//            var sql = @"
//                SELECT * FROM StoryboardFrame 
//                WHERE ProjectId = @ProjectId 
//                ORDER BY FrameIndex ASC";
//            return await connection.QueryAsync<StoryboardFrame>(sql, new { ProjectId = projectId });
//        }

//        public async Task<bool> UpdateFrameAsync(StoryboardFrame frame)
//        {
//            using var connection = new MySqlConnection(_connectionString);
//            var sql = @"
//                UPDATE StoryboardFrame 
//                SET FramePrompt = @FramePrompt, 
//                    FrameReferenceImages = @FrameReferenceImages,
//                    TaskId = @TaskId,
//                    Status = @Status,
//                    ResultImageUrl = @ResultImageUrl,
//                    ThumbnailUrl = @ThumbnailUrl,
//                    ErrorMessage = @ErrorMessage,
//                    Progress = @Progress,
//                    ProgressMessage = @ProgressMessage,
//                    CompletedAt = @CompletedAt
//                WHERE Id = @Id";

//            var result = await connection.ExecuteAsync(sql, frame);
//            return result > 0;
//        }

//        public async Task<bool> DeleteFrameAsync(int id)
//        {
//            using var connection = new MySqlConnection(_connectionString);
//            var sql = "DELETE FROM StoryboardFrame WHERE Id = @Id";
//            var result = await connection.ExecuteAsync(sql, new { Id = id });
//            return result > 0;
//        }

//        public async Task<bool> DeleteProjectFramesAsync(int projectId)
//        {
//            using var connection = new MySqlConnection(_connectionString);
//            var sql = "DELETE FROM StoryboardFrame WHERE ProjectId = @ProjectId";
//            var result = await connection.ExecuteAsync(sql, new { ProjectId = projectId });
//            return result > 0;
//        }

//        // ===== 查询和统计 =====

//        public async Task<int> GetProjectFrameCountAsync(int projectId)
//        {
//            using var connection = new MySqlConnection(_connectionString);
//            var sql = "SELECT COUNT(*) FROM StoryboardFrame WHERE ProjectId = @ProjectId";
//            return await connection.ExecuteScalarAsync<int>(sql, new { ProjectId = projectId });
//        }

//        public async Task<int> GetProjectCompletedFrameCountAsync(int projectId)
//        {
//            using var connection = new MySqlConnection(_connectionString);
//            var sql = "SELECT COUNT(*) FROM StoryboardFrame WHERE ProjectId = @ProjectId AND Status = 'Completed'";
//            return await connection.ExecuteScalarAsync<int>(sql, new { ProjectId = projectId });
//        }

//        public async Task<StoryboardFrame?> GetFrameByTaskIdAsync(int taskId)
//        {
//            using var connection = new MySqlConnection(_connectionString);
//            var sql = "SELECT * FROM StoryboardFrame WHERE TaskId = @TaskId";
//            return await connection.QueryFirstOrDefaultAsync<StoryboardFrame>(sql, new { TaskId = taskId });
//        }

//        public async Task<IEnumerable<StoryboardFrame>> GetPendingFramesAsync(int limit = 50)
//        {
//            using var connection = new MySqlConnection(_connectionString);
//            var sql = @"
//                SELECT * FROM StoryboardFrame 
//                WHERE Status IN ('Pending', 'Processing') 
//                ORDER BY CreatedAt ASC 
//                LIMIT @Limit";
//            return await connection.QueryAsync<StoryboardFrame>(sql, new { Limit = limit });
//        }

//        // ===== 模板相关 =====

//        public async Task<int> CreateTemplateAsync(StoryboardTemplate template)
//        {
//            using var connection = new MySqlConnection(_connectionString);
//            var sql = @"
//                INSERT INTO StoryboardTemplate (
//                    ProjectId, ShareCode, CreatorId, TemplateName, TemplateDescription, 
//                    DownloadCount, CreatedAt, UpdatedAt, IsPublic
//                ) VALUES (
//                    @ProjectId, @ShareCode, @CreatorId, @TemplateName, @TemplateDescription,
//                    @DownloadCount, @CreatedAt, @UpdatedAt, @IsPublic
//                );
//                SELECT LAST_INSERT_ID();";

//            template.CreatedAt = DateTime.Now;
//            template.UpdatedAt = DateTime.Now;

//            return await connection.ExecuteScalarAsync<int>(sql, template);
//        }

//        public async Task<StoryboardTemplate?> GetTemplateByShareCodeAsync(string shareCode)
//        {
//            using var connection = new MySqlConnection(_connectionString);
//            var sql = "SELECT * FROM StoryboardTemplate WHERE ShareCode = @ShareCode AND IsPublic = TRUE";
//            return await connection.QueryFirstOrDefaultAsync<StoryboardTemplate>(sql, new { ShareCode = shareCode });
//        }

//        public async Task<bool> UpdateTemplateDownloadCountAsync(string shareCode)
//        {
//            using var connection = new MySqlConnection(_connectionString);
//            var sql = @"
//                UPDATE StoryboardTemplate 
//                SET DownloadCount = DownloadCount + 1,
//                    UpdatedAt = @UpdatedAt
//                WHERE ShareCode = @ShareCode";

//            var result = await connection.ExecuteAsync(sql, new { ShareCode = shareCode, UpdatedAt = DateTime.Now });
//            return result > 0;
//        }
//    }
//}
