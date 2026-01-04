// ===== 全局变量 =====
let currentProject = null;
let currentFrames = [];
let projectTemplates = [];
let framePollingIntervals = {};
let storyboardInitialized = false;

// ===== 初始化 =====
document.addEventListener('DOMContentLoaded', async function() {
    // 如果是独立的 Storyboard 页面，立即初始化
    if (document.getElementById('storyboard-content') && !document.getElementById('drawing-content')) {
        await initStoryboard();
    }
});

async function initStoryboard() {
    if (storyboardInitialized) return;
    storyboardInitialized = true;

    // 加载模型列表
    await loadModels();
    
    // 加载模板
    loadTemplates();
    
    // 加载项目列表
    await loadProjects();
    
    // 添加图片上传事件处理
    bindImageUploadEvents();
    
    // 初始化回到顶部按钮
    initScrollToTopButton();
}

// ===== 项目管理 =====

async function loadProjects() {
    try {
        const response = await fetch('/api/storyboard/projects?pageSize=100&offset=0', {
            headers: {
                'Authorization': `Bearer ${getToken()}`
            }
        });

        if (!response.ok) {
            showToast('加载项目失败', 'error');
            return;
        }

        const data = await response.json();
        const projectSelect = document.getElementById('projectSelect');
        
        // 清空选项（保留"创建新项目"）
        projectSelect.innerHTML = '<option value="">创建新项目</option>';
        
        if (data.data && data.data.length > 0) {
            data.data.forEach(project => {
                const option = document.createElement('option');
                option.value = project.id;
                option.textContent = `${project.projectName} (${project.completedFrames}/${project.totalFrames})`;
                projectSelect.appendChild(option);
            });
        }
    } catch (error) {
        console.error('加载项目列表失败:', error);
        showToast('加载项目列表失败', 'error');
    }
}

async function loadProjectDetails() {
    const projectId = document.getElementById('projectSelect').value;
    
    if (!projectId) {
        // 创建新项目
        clearProjectForm();
        currentProject = null;
        currentFrames = [];
        updateFramesContainer();
        return;
    }

    try {
        const response = await fetch(`/api/storyboard/projects/${projectId}`, {
            headers: {
                'Authorization': `Bearer ${getToken()}`
            }
        });

        if (!response.ok) {
            showToast('加载项目详情失败', 'error');
            return;
        }

        const data = await response.json();
        currentProject = data.data.project;
        currentFrames = data.data.frames;

        // 填充项目信息
        document.getElementById('projectName').value = currentProject.projectName;
        document.getElementById('projectDescription').value = currentProject.description || '';
        document.getElementById('modelSelect').value = currentProject.modelId;
        document.getElementById('aspectRatioSelect').value = currentProject.aspectRatio;
        document.getElementById('basePrompt').value = currentProject.basePrompt || '';

        // 显示参考图
        if (currentProject.referenceImages && currentProject.referenceImages.length > 0) {
            renderReferenceImages(currentProject.referenceImages);
        }

        // 更新分镜列表
        updateFramesContainer();
        updateFrameProgress();

    } catch (error) {
        console.error('加载项目详情失败:', error);
        showToast('加载项目详情失败', 'error');
    }
}

async function saveProject() {
    const projectName = document.getElementById('projectName').value.trim();
    const projectDescription = document.getElementById('projectDescription').value.trim();
    const modelId = parseInt(document.getElementById('modelSelect').value);
    const aspectRatio = document.getElementById('aspectRatioSelect').value;
    const basePrompt = document.getElementById('basePrompt').value.trim();

    if (!projectName) {
        showToast('请输入项目名称', 'warning');
        return;
    }

    if (!modelId) {
        showToast('请选择AI模型', 'warning');
        return;
    }

    try {
        const referenceImages = document.querySelectorAll('#referenceImagePreview img');
        const refImageUrls = Array.from(referenceImages).map(img => img.src);

        if (currentProject) {
            // 更新项目
            const response = await fetch(`/api/storyboard/projects/${currentProject.id}`, {
                method: 'PUT',
                headers: {
                    'Authorization': `Bearer ${getToken()}`,
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    projectName,
                    description: projectDescription,
                    aspectRatio,
                    basePrompt,
                    referenceImages: refImageUrls
                })
            });

            if (!response.ok) {
                showToast('更新项目失败', 'error');
                return;
            }

            showToast('项目已更新', 'success');
        } else {
            // 创建新项目
            const response = await fetch('/api/storyboard/projects', {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${getToken()}`,
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    projectName,
                    description: projectDescription,
                    modelId,
                    aspectRatio,
                    basePrompt,
                    referenceImages: refImageUrls
                })
            });

            if (!response.ok) {
                showToast('创建项目失败', 'error');
                return;
            }

            const data = await response.json();
            currentProject = { id: data.projectId };
            showToast('项目已创建', 'success');
            
            // 重新加载项目列表
            await loadProjects();
            document.getElementById('projectSelect').value = data.projectId;
        }

        await loadProjectDetails();
    } catch (error) {
        console.error('保存项目失败:', error);
        showToast('保存项目失败', 'error');
    }
}

async function deleteCurrentProject() {
    if (!currentProject || !currentProject.id) {
        showToast('请先选择项目', 'warning');
        return;
    }

    if (!confirm('确定要删除此项目及其所有分镜吗？')) {
        return;
    }

    try {
        const response = await fetch(`/api/storyboard/projects/${currentProject.id}`, {
            method: 'DELETE',
            headers: {
                'Authorization': `Bearer ${getToken()}`
            }
        });

        if (!response.ok) {
            showToast('删除项目失败', 'error');
            return;
        }

        showToast('项目已删除', 'success');
        currentProject = null;
        currentFrames = [];
        clearProjectForm();
        updateFramesContainer();
        await loadProjects();
    } catch (error) {
        console.error('删除项目失败:', error);
        showToast('删除项目失败', 'error');
    }
}

function clearProjectForm() {
    document.getElementById('projectName').value = '';
    document.getElementById('projectDescription').value = '';
    document.getElementById('basePrompt').value = '';
    document.getElementById('referenceImagePreview').style.display = 'none';
    document.getElementById('referenceImagePreview').innerHTML = '';
}

async function exportProject() {
    if (!currentProject || !currentProject.id) {
        showToast('请先选择项目', 'warning');
        return;
    }

    try {
        const response = await fetch(`/api/storyboard/projects/${currentProject.id}/export`, {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${getToken()}`
            }
        });

        if (!response.ok) {
            showToast('导出项目失败', 'error');
            return;
        }

        // 下载文件
        const blob = await response.blob();
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `storyboard_${currentProject.id}_${new Date().getTime()}.json`;
        document.body.appendChild(a);
        a.click();
        window.URL.revokeObjectURL(url);
        document.body.removeChild(a);

        showToast('项目已导出', 'success');
    } catch (error) {
        console.error('导出项目失败:', error);
        showToast('导出项目失败', 'error');
    }
}

// ===== 分镜帧管理 =====

function addNewFrame() {
    if (!currentProject || !currentProject.id) {
        showToast('请先创建并保存项目', 'warning');
        return;
    }

    document.getElementById('editFrameId').value = '';
    document.getElementById('frameIndex').value = currentFrames.length + 1;
    document.getElementById('framePrompt').value = '';
    document.getElementById('frameReferenceImagePreview').innerHTML = '';
    document.getElementById('frameReferenceImagePreview').style.display = 'none';
    document.getElementById('frameEditorTitle').textContent = '添加分镜帧';
    document.getElementById('frameEditorModal').style.display = 'flex';
}

function closeFrameEditor() {
    document.getElementById('frameEditorModal').style.display = 'none';
}

async function saveFrame() {
    const frameIndex = parseInt(document.getElementById('frameIndex').value);
    const framePrompt = document.getElementById('framePrompt').value.trim();
    const editFrameId = document.getElementById('editFrameId').value;

    if (!frameIndex || frameIndex < 1) {
        showToast('请输入有效的帧序号', 'warning');
        return;
    }

    if (!framePrompt) {
        showToast('请输入帧提示词', 'warning');
        return;
    }

    try {
        const referenceImages = document.querySelectorAll('#frameReferenceImagePreview img');
        const refImageUrls = Array.from(referenceImages).map(img => img.src);

        if (editFrameId) {
            // 编辑现有帧 - 暂不支持，提示用户删除后重新添加
            showToast('分镜生成后无法编辑，请删除后重新添加', 'warning');
            return;
        } else {
            // 添加新帧
            const response = await fetch('/api/storyboard/frames', {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${getToken()}`,
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    projectId: currentProject.id,
                    frameIndex,
                    framePrompt,
                    referenceImages: refImageUrls.length > 0 ? refImageUrls : null
                })
            });

            if (!response.ok) {
                const error = await response.json();
                showToast(error.message || '添加分镜帧失败', 'error');
                return;
            }

            showToast('分镜帧已添加', 'success');
            closeFrameEditor();
            await loadProjectDetails();
        }
    } catch (error) {
        console.error('保存分镜帧失败:', error);
        showToast('保存分镜帧失败', 'error');
    }
}

async function deleteFrame(frameId) {
    if (!confirm('确定要删除此分镜帧吗？')) {
        return;
    }

    try {
        const response = await fetch(`/api/storyboard/frames/${frameId}`, {
            method: 'DELETE',
            headers: {
                'Authorization': `Bearer ${getToken()}`
            }
        });

        if (!response.ok) {
            showToast('删除分镜帧失败', 'error');
            return;
        }

        showToast('分镜帧已删除', 'success');
        await loadProjectDetails();
    } catch (error) {
        console.error('删除分镜帧失败:', error);
        showToast('删除分镜帧失败', 'error');
    }
}

// ===== 分镜生成 =====

async function generateFrame(frameId) {
    try {
        const response = await fetch('/api/storyboard/generate-frame', {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${getToken()}`,
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ frameId })
        });

        if (!response.ok) {
            const error = await response.json();
            showToast(error.message || '生成分镜失败', 'error');
            return;
        }

        const data = await response.json();
        showToast('生成任务已提交', 'success');

        // 开始轮询进度
        pollFrameProgress(frameId, data.taskId);
    } catch (error) {
        console.error('生成分镜失败:', error);
        showToast('生成分镜失败', 'error');
    }
}

async function pollFrameProgress(frameId, taskId) {
    // 清除已有的轮询
    if (framePollingIntervals[frameId]) {
        clearInterval(framePollingIntervals[frameId]);
    }

    // 设置新的轮询
    framePollingIntervals[frameId] = setInterval(async () => {
        try {
            const response = await fetch(`/api/storyboard/frame-progress/${frameId}`, {
                headers: {
                    'Authorization': `Bearer ${getToken()}`
                }
            });

            if (!response.ok) {
                return;
            }

            const data = await response.json();
            const frame = currentFrames.find(f => f.id === frameId);
            
            if (frame) {
                frame.status = data.status;
                frame.progress = data.progress;
                frame.progressMessage = data.progressMessage;
                frame.resultImageUrl = data.resultImageUrl;
                frame.thumbnailUrl = data.thumbnailUrl;

                updateFramesContainer();
                updateFrameProgress();

                // 如果完成或失败，停止轮询
                if (data.status === 'Completed' || data.status === 'Failed') {
                    clearInterval(framePollingIntervals[frameId]);
                    delete framePollingIntervals[frameId];
                }
            }
        } catch (error) {
            console.error('获取分镜进度失败:', error);
        }
    }, 2000); // 每2秒查询一次
}

// ===== UI 渲染 =====

function updateFramesContainer() {
    const container = document.getElementById('framesContainer');

    if (!currentProject || currentFrames.length === 0) {
        container.innerHTML = `
            <div class="empty-state">
                <i class="fas fa-film"></i>
                <p>暂无分镜</p>
                <p style="font-size: 0.875rem; margin-top: var(--spacing-sm);">创建项目后添加分镜帧</p>
            </div>
        `;
        return;
    }

    container.innerHTML = currentFrames.map((frame, index) => `
        <div class="storyboard-frame-card" onclick="previewFrame(${frame.id})">
            <div class="frame-thumbnail">
                ${frame.resultImageUrl ? `
                    <img src="${frame.thumbnailUrl || frame.resultImageUrl}" alt="帧 ${frame.frameIndex}">
                    <div class="frame-status-badge" style="background: #4caf50;">已完成</div>
                ` : `
                    <div class="frame-placeholder">
                        <i class="fas fa-image"></i>
                    </div>
                    <div class="frame-status-badge" style="background: ${getStatusColor(frame.status)};">${getStatusText(frame.status)}</div>
                `}
            </div>
            <div class="frame-info">
                <h4>第 ${frame.frameIndex} 帧</h4>
                <p class="frame-prompt" title="${frame.framePrompt}">${frame.framePrompt}</p>
                ${frame.status !== 'Completed' && frame.status !== 'Failed' ? `
                    <div class="progress-bar" style="margin-top: 8px;">
                        <div class="progress-fill" style="width: ${frame.progress}%"></div>
                    </div>
                    <small>${frame.progressMessage || '等待中...'}</small>
                ` : ''}
            </div>
            <div class="frame-actions">
                ${frame.resultImageUrl ? `
                    <button class="btn btn-small" onclick="event.stopPropagation(); splitUpscaleFrame(${frame.id})" title="拆分重绘">
                        <i class="fas fa-th"></i>
                    </button>
                ` : `
                    <button class="btn btn-small" onclick="event.stopPropagation(); generateFrame(${frame.id})" title="生成">
                        <i class="fas fa-magic"></i>
                    </button>
                `}
                <button class="btn btn-small" onclick="event.stopPropagation(); deleteFrame(${frame.id})" title="删除">
                    <i class="fas fa-trash"></i>
                </button>
            </div>
        </div>
    `).join('');
}

function updateFrameProgress() {
    if (!currentProject) return;

    const completed = currentFrames.filter(f => f.status === 'Completed').length;
    const total = currentFrames.length;
    const progressText = document.getElementById('frameProgress');

    if (total === 0) {
        progressText.textContent = '暂无分镜';
    } else {
        progressText.textContent = `已完成 ${completed}/${total} 帧`;
    }
}

function previewFrame(frameId) {
    const frame = currentFrames.find(f => f.id === frameId);
    if (!frame) return;

    document.getElementById('previewFrameIndex').textContent = frame.frameIndex;
    document.getElementById('previewStatus').textContent = getStatusText(frame.status);
    document.getElementById('previewProgress').textContent = `${frame.progress}%`;
    document.getElementById('previewPrompt').textContent = frame.framePrompt;

    if (frame.resultImageUrl) {
        document.getElementById('previewImage').src = frame.resultImageUrl;
        document.getElementById('framePreviewSplitBtn').style.display = 'inline-block';
    } else {
        document.getElementById('previewImage').src = '';
        document.getElementById('framePreviewSplitBtn').style.display = 'none';
    }

    document.getElementById('framePreviewModal').style.display = 'flex';
}

function closeFramePreview() {
    document.getElementById('framePreviewModal').style.display = 'none';
}

function reopenFrameEditor() {
    closeFramePreview();
    // 暂不支持编辑
    showToast('分镜生成后无法编辑，请删除后重新添加', 'warning');
}

function splitUpscaleFrame(frameId) {
    const frame = currentFrames.find(f => f.id === frameId);
    if (!frame || !frame.resultImageUrl) {
        showToast('此分镜还未生成完成', 'warning');
        return;
    }

    // 调用拆分重绘功能
    const taskId = frame.taskId || frameId; // 使用任务ID或帧ID
    openSplitUpscaleModal(taskId, frame.resultImageUrl);
    
    closeFramePreview();
}

// ===== 辅助函数 =====

function getStatusText(status) {
    const statusMap = {
        'Pending': '等待中',
        'Processing': '处理中',
        'Completed': '已完成',
        'Failed': '失败'
    };
    return statusMap[status] || status;
}

function getStatusColor(status) {
    const colorMap = {
        'Pending': '#ff9800',
        'Processing': '#2196f3',
        'Completed': '#4caf50',
        'Failed': '#f44336'
    };
    return colorMap[status] || '#999';
}

function renderReferenceImages(imageUrls) {
    const container = document.getElementById('referenceImagePreview');
    if (!imageUrls || imageUrls.length === 0) {
        container.style.display = 'none';
        return;
    }

    container.innerHTML = imageUrls.map((url, index) => `
        <div class="preview-item">
            <img src="${url}" alt="参考图 ${index + 1}">
            <button class="btn-remove" onclick="removeReferenceImage(${index})" title="移除">×</button>
        </div>
    `).join('');
    container.style.display = 'flex';
}

function removeReferenceImage(index) {
    const container = document.getElementById('referenceImagePreview');
    const images = container.querySelectorAll('img');
    if (images[index]) {
        images[index].closest('.preview-item').remove();
    }
}

// ===== 图片上传处理 =====

function bindImageUploadEvents() {
    // 项目参考图上传
    document.getElementById('referenceImageInput').addEventListener('change', function(e) {
        handleImageUpload(e, 'referenceImagePreview');
    });

    // 分镜帧参考图上传
    document.getElementById('frameReferenceImageInput').addEventListener('change', function(e) {
        handleImageUpload(e, 'frameReferenceImagePreview');
    });

    // 拖拽上传支持
    addDragDropSupport('referenceImageInput', 'referenceImagePreview');
    addDragDropSupport('frameReferenceImageInput', 'frameReferenceImagePreview');
}

function handleImageUpload(event, previewContainerId) {
    const files = event.target.files;
    const container = document.getElementById(previewContainerId);
    
    Array.from(files).forEach(file => {
        if (!file.type.startsWith('image/')) {
            return;
        }

        const reader = new FileReader();
        reader.onload = function(e) {
            const img = document.createElement('img');
            img.src = e.target.result;
            
            const item = document.createElement('div');
            item.className = 'preview-item';
            item.innerHTML = `
                <img src="${e.target.result}" alt="预览">
                <button class="btn-remove" onclick="this.parentElement.remove()" title="移除">×</button>
            `;
            
            container.appendChild(item);
        };
        reader.readAsDataURL(file);
    });

    container.style.display = 'flex';
    event.target.value = '';
}

function addDragDropSupport(inputId, previewContainerId) {
    const input = document.getElementById(inputId);
    const uploadArea = input.previousElementSibling;

    ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
        uploadArea.addEventListener(eventName, preventDefaults, false);
    });

    function preventDefaults(e) {
        e.preventDefault();
        e.stopPropagation();
    }

    ['dragenter', 'dragover'].forEach(eventName => {
        uploadArea.addEventListener(eventName, () => {
            uploadArea.style.backgroundColor = 'var(--bg-hover)';
        });
    });

    ['dragleave', 'drop'].forEach(eventName => {
        uploadArea.addEventListener(eventName, () => {
            uploadArea.style.backgroundColor = 'transparent';
        });
    });

    uploadArea.addEventListener('drop', (e) => {
        const files = e.dataTransfer.files;
        input.files = files;
        const event = new Event('change', { bubbles: true });
        input.dispatchEvent(event);
    });
}

// ===== 模型加载 =====

async function loadModels() {
    try {
        const response = await fetch('/api/models', {
            headers: {
                'Authorization': `Bearer ${getToken()}`
            }
        });

        if (!response.ok) {
            return;
        }

        const data = await response.json();
        const select = document.getElementById('modelSelect');
        select.innerHTML = '';

        if (data.length > 0) {
            data.forEach(model => {
                const option = document.createElement('option');
                option.value = model.id;
                option.textContent = `${model.modelName} (${model.pointCost}积分)`;
                select.appendChild(option);
            });
        }
    } catch (error) {
        console.error('加载模型失败:', error);
    }
}

// ===== 模板管理 =====

function loadTemplates() {
    const stored = localStorage.getItem('storyboardTemplates');
    projectTemplates = stored ? JSON.parse(stored) : [];
    renderTemplates();
}

function renderTemplates() {
    const container = document.getElementById('promptTemplates');
    
    if (projectTemplates.length === 0) {
        container.innerHTML = '<small style="color: var(--text-secondary);">暂无模板</small>';
        return;
    }

    container.innerHTML = projectTemplates.map((template, index) => `
        <div class="template-item">
            <button class="template-btn" onclick="applyTemplate(${index})" title="应用此模板">
                ${template.name}
            </button>
            <button class="btn-remove" onclick="deleteTemplate(${index})" title="删除">×</button>
        </div>
    `).join('');
}

function applyTemplate(index) {
    const template = projectTemplates[index];
    document.getElementById('basePrompt').value = template.content;
    showToast('模板已应用', 'success');
}

function deleteTemplate(index) {
    projectTemplates.splice(index, 1);
    saveTemplates();
    renderTemplates();
    showToast('模板已删除', 'success');
}

function addCustomTemplate() {
    const prompt = document.getElementById('basePrompt').value.trim();
    
    if (!prompt) {
        showToast('请先编写提示词', 'warning');
        return;
    }

    const name = prompt('输入模板名称:', '');
    if (!name) return;

    projectTemplates.push({ name, content: prompt });
    saveTemplates();
    renderTemplates();
    showToast('模板已保存', 'success');
}

function resetTemplates() {
    if (!confirm('确定要重置所有模板吗？')) {
        return;
    }
    projectTemplates = [];
    saveTemplates();
    renderTemplates();
    showToast('模板已重置', 'success');
}

function saveTemplates() {
    localStorage.setItem('storyboardTemplates', JSON.stringify(projectTemplates));
}

// ===== 通用函数 =====

function getToken() {
    return localStorage.getItem('token') || sessionStorage.getItem('token') || '';
}

function showToast(message, type = 'info') {
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.innerHTML = `<i class="fas fa-${type === 'success' ? 'check-circle' : type === 'error' ? 'exclamation-circle' : 'info-circle'}"></i> ${message}`;
    document.body.appendChild(toast);

    setTimeout(() => {
        toast.classList.add('show');
    }, 10);

    setTimeout(() => {
        toast.classList.remove('show');
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

function initScrollToTopButton() {
    const btn = document.getElementById('scrollToTopBtn');
    window.addEventListener('scroll', () => {
        btn.style.display = window.scrollY > 300 ? 'block' : 'none';
    });
}

function scrollToTop() {
    window.scrollTo({ top: 0, behavior: 'smooth' });
}
