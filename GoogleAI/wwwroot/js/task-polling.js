let pollingInterval = null;

// 轮询任务状态
function pollTaskStatus(taskId) {
    let attempts = 0;
    const maxAttempts = 180; // 最多轮询3分钟(每2秒一次)
    const processMessages = [
        '🎨 AI正在理解您的创意...',
        '🧠 深度学习网络正在工作...',
        '✨ 正在生成创意草图...',
        '🎯 AI正在精细化处理...',
        '🖌️ 正在进行细节渲染...',
        '🌟 即将完成，请稍候...'
    ];

    let messageIndex = 0;

    pollingInterval = setInterval(async () => {
        attempts++;

        if (attempts > maxAttempts) {
            clearInterval(pollingInterval);
            hideLoading();
            showError('任务处理超时,请稍后在历史记录中查看结果');
            return;
        }

        try {
            const response = await fetch(`/api/tasks/${taskId}/status`, {
                headers: {
                    'Authorization': 'Bearer ' + getToken()
                }
            });

            const status = await response.json();

            if (status.status === 'Completed') {
                clearInterval(pollingInterval);
                hideLoading();
                displayResult(status.resultImageUrl);
            } else if (status.status === 'Failed') {
                clearInterval(pollingInterval);
                hideLoading();
                showError('生成失败: ' + (status.errorMessage || '未知错误，请重试'));
            } else if (status.status === 'Processing') {
                // 更新进度提示
                messageIndex = (messageIndex + 1) % processMessages.length;
                $('#loadingStatusText').text(processMessages[messageIndex]);

                const currentProgress = Math.min((attempts / maxAttempts) * 100, 90);
                updateProgressBar(currentProgress);
                $('#progressText').text(`处理进度: ${Math.floor(currentProgress)}%`);
            }
        } catch (error) {

            // 继续轮询,不中断
        }
    }, 2000); // 每2秒查询一次
}

// 更新进度条
function updateProgressBar(percent) {
    $('#progressBar').css('width', percent + '%');
}

// 停止轮询
function stopPolling() {
    if (pollingInterval) {
        clearInterval(pollingInterval);
        pollingInterval = null;
    }
}

// 页面卸载时停止轮询
$(window).on('beforeunload', function () {
    stopPolling();
});