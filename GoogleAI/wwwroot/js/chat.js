class ChatManager {
    constructor() {
        this.currentChatId = null;
        this.currentModelId = null;
        this.uploadedImages = [];
        this.token = localStorage.getItem('token');
        this.init();
    }

    init() {
        this.setupEventListeners();
        this.loadModels();
        this.loadChats();
    }

    setupEventListeners() {
        // 新建对话
        document.getElementById('newChatBtn').addEventListener('click', () => this.showNewChatModal());
        document.getElementById('confirmBtn').addEventListener('click', () => this.createChat());
        document.getElementById('cancelBtn').addEventListener('click', () => this.closeModal());
        document.getElementById('closeModalBtn').addEventListener('click', () => this.closeModal());

        // 消息输入
        document.getElementById('sendBtn').addEventListener('click', () => this.sendMessage());
        document.getElementById('messageInput').addEventListener('keydown', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                this.sendMessage();
            }
        });

        // 图片上传
        document.getElementById('uploadBtn').addEventListener('click', () => {
            document.getElementById('imageInput').click();
        });
        document.getElementById('imageInput').addEventListener('change', (e) => this.handleImageUpload(e));

        // 模型切换
        document.getElementById('modelSelect').addEventListener('change', (e) => {
            this.currentModelId = parseInt(e.target.value);
        });
    }

    async loadModels() {
        try {
            const response = await fetch('/api/chat/models', {
                headers: { 'Authorization': `Bearer ${this.token}` }
            });
            const models = await response.json();

            const modelSelect = document.getElementById('modelSelect');
            const newChatModelSelect = document.getElementById('newChatModelSelect');

            modelSelect.innerHTML = '';
            newChatModelSelect.innerHTML = '';

            models.forEach(model => {
                const option1 = document.createElement('option');
                option1.value = model.id;
                option1.textContent = `${model.modelName} (${model.pointCost}积分)`;
                modelSelect.appendChild(option1);

                const option2 = document.createElement('option');
                option2.value = model.id;
                option2.textContent = `${model.modelName} (${model.pointCost}积分)`;
                newChatModelSelect.appendChild(option2);
            });

            if (models.length > 0) {
                this.currentModelId = models[0].id;
                modelSelect.value = models[0].id;
                newChatModelSelect.value = models[0].id;
            }
        } catch (error) {
            console.error('加载模型失败:', error);
            this.showError('加载模型失败');
        }
    }

    async loadChats() {
        try {
            const response = await fetch('/api/chat/list', {
                headers: { 'Authorization': `Bearer ${this.token}` }
            });
            const chats = await response.json();

            const chatList = document.getElementById('chatList');
            chatList.innerHTML = '';

            chats.forEach(chat => {
                const chatItem = document.createElement('div');
                chatItem.className = 'chat-item';
                chatItem.innerHTML = `
                    <div class="chat-item-title">${chat.title}</div>
                    <span class="chat-item-delete">×</span>
                `;

                chatItem.addEventListener('click', (e) => {
                    if (e.target.classList.contains('chat-item-delete')) {
                        this.deleteChat(chat.id);
                    } else {
                        this.selectChat(chat.id);
                    }
                });

                chatList.appendChild(chatItem);
            });
        } catch (error) {
            console.error('加载对话列表失败:', error);
        }
    }

    showNewChatModal() {
        document.getElementById('newChatModal').classList.add('show');
    }

    closeModal() {
        document.getElementById('newChatModal').classList.remove('show');
    }

    async createChat() {
        const title = document.getElementById('chatTitleInput').value || '新建对话';
        const modelId = parseInt(document.getElementById('newChatModelSelect').value);

        if (!modelId) {
            this.showError('请选择模型');
            return;
        }

        try {
            const response = await fetch('/api/chat/create', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${this.token}`
                },
                body: JSON.stringify({ modelId, title })
            });

            if (!response.ok) throw new Error('创建对话失败');

            const chat = await response.json();
            this.closeModal();
            this.loadChats();
            this.selectChat(chat.id);
        } catch (error) {
            console.error('创建对话失败:', error);
            this.showError('创建对话失败');
        }
    }

    async selectChat(chatId) {
        this.currentChatId = chatId;

        // 更新UI
        document.querySelectorAll('.chat-item').forEach(item => {
            item.classList.remove('active');
        });
        event.currentTarget.classList.add('active');

        try {
            const response = await fetch(`/api/chat/${chatId}`, {
                headers: { 'Authorization': `Bearer ${this.token}` }
            });
            const chat = await response.json();

            document.getElementById('chatTitle').textContent = chat.title;
            document.getElementById('modelSelect').value = chat.modelId;
            this.currentModelId = chat.modelId;

            this.displayMessages(chat.messages);
        } catch (error) {
            console.error('加载对话失败:', error);
            this.showError('加载对话失败');
        }
    }

    displayMessages(messages) {
        const messagesContainer = document.getElementById('chatMessages');
        messagesContainer.innerHTML = '';

        if (messages.length === 0) {
            messagesContainer.innerHTML = `
                <div class="welcome-message">
                    <h2>开始对话</h2>
                    <p>输入消息开始与AI对话</p>
                </div>
            `;
            return;
        }

        messages.forEach(msg => {
            this.addMessageToUI(msg.role, msg.content, msg.imageUrls);
        });

        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }

    addMessageToUI(role, content, imageUrls = []) {
        const messagesContainer = document.getElementById('chatMessages');

        // 移除欢迎消息
        const welcomeMsg = messagesContainer.querySelector('.welcome-message');
        if (welcomeMsg) welcomeMsg.remove();

        const messageDiv = document.createElement('div');
        messageDiv.className = `message ${role}`;

        const contentDiv = document.createElement('div');
        contentDiv.className = 'message-content';
        contentDiv.textContent = content;

        messageDiv.appendChild(contentDiv);

        // 添加图片
        if (imageUrls && imageUrls.length > 0) {
            const imagesDiv = document.createElement('div');
            imagesDiv.className = 'message-images';

            imageUrls.forEach(url => {
                const img = document.createElement('img');
                img.src = url;
                img.className = 'message-image';
                img.addEventListener('click', () => this.viewImage(url));
                imagesDiv.appendChild(img);
            });

            messageDiv.appendChild(imagesDiv);
        }

        messagesContainer.appendChild(messageDiv);
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }

    async sendMessage() {
        const content = document.getElementById('messageInput').value.trim();

        if (!content && this.uploadedImages.length === 0) {
            this.showError('请输入消息或上传图片');
            return;
        }

        if (!this.currentChatId) {
            this.showError('请先选择或创建对话');
            return;
        }

        // 保存图片URL列表（在清空前）
        const imageUrls = [...this.uploadedImages];

        // 显示用户消息
        this.addMessageToUI('user', content, imageUrls);

        // 清空输入
        document.getElementById('messageInput').value = '';
        this.uploadedImages = [];
        document.getElementById('uploadedImages').innerHTML = '';

        // 禁用发送按钮
        document.getElementById('sendBtn').disabled = true;

        try {
            const useStreaming = document.getElementById('useStreamingCheckbox').checked;
            const useContext = document.getElementById('useContextCheckbox').checked;
            const contextWindowSize = parseInt(document.getElementById('contextWindowSize').value);

            const request = {
                chatId: this.currentChatId,
                content,
                imageUrls: imageUrls,
                useContext,
                contextWindowSize,
                useStreaming
            };

            if (useStreaming) {
                await this.sendMessageStream(request);
            } else {
                await this.sendMessageNormal(request);
            }
        } catch (error) {
            console.error('发送消息失败:', error);
            this.showError('发送消息失败');
        } finally {
            document.getElementById('sendBtn').disabled = false;
        }
    }

    async sendMessageNormal(request) {
        const response = await fetch('/api/chat/message', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${this.token}`
            },
            body: JSON.stringify(request)
        });

        if (!response.ok) throw new Error('发送失败');

        const message = await response.json();
        this.addMessageToUI('assistant', message.content, []);
    }

    async sendMessageStream(request) {
        const response = await fetch('/api/chat/message-stream', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${this.token}`
            },
            body: JSON.stringify(request)
        });

        if (!response.ok) throw new Error('发送失败');

        const messagesContainer = document.getElementById('chatMessages');
        let currentMessage = null;
        let fullContent = '';
        let messageStarted = false;

        const reader = response.body.getReader();
        const decoder = new TextDecoder();

        try {
            while (true) {
                const { done, value } = await reader.read();
                if (done) break;

                const text = decoder.decode(value, { stream: true });
                const lines = text.split('\n');

                for (const line of lines) {
                    if (line.trim().startsWith('data: ')) {
                        try {
                            const jsonStr = line.slice(6).trim();
                            if (!jsonStr) continue;
                            
                            const event = JSON.parse(jsonStr);
                            
                            // 处理大小写问题：API返回的是Type/Content，需要转换为type/content
                            const eventType = event.type || event.Type;
                            const eventContent = event.content || event.Content;
                            const eventError = event.error || event.Error;

                            console.log('收到事件:', { eventType, eventContent, eventError });

                            if (eventType === 'start') {
                                // 移除欢迎消息
                                const welcomeMsg = messagesContainer.querySelector('.welcome-message');
                                if (welcomeMsg) welcomeMsg.remove();

                                // 创建新消息容器
                                currentMessage = document.createElement('div');
                                currentMessage.className = 'message assistant';
                                const contentDiv = document.createElement('div');
                                contentDiv.className = 'message-content';
                                contentDiv.textContent = '';
                                currentMessage.appendChild(contentDiv);
                                messagesContainer.appendChild(currentMessage);
                                messageStarted = true;
                                fullContent = '';
                            } else if (eventType === 'content') {
                                // 追加内容
                                if (messageStarted && eventContent) {
                                    fullContent += eventContent;
                                    if (currentMessage) {
                                        const contentDiv = currentMessage.querySelector('.message-content');
                                        contentDiv.textContent = fullContent;
                                        messagesContainer.scrollTop = messagesContainer.scrollHeight;
                                    }
                                }
                            } else if (eventType === 'end') {
                                // 完成
                                messageStarted = false;
                                messagesContainer.scrollTop = messagesContainer.scrollHeight;
                            } else if (eventType === 'error') {
                                console.error('AI返回错误:', eventError);
                                this.showError(eventError || '发生错误');
                            }
                        } catch (e) {
                            console.error('解析事件失败:', e, '行内容:', line);
                        }
                    }
                }
            }
        } catch (error) {
            console.error('流式读取失败:', error);
            this.showError('流式读取失败: ' + error.message);
        }
    }

    handleImageUpload(e) {
        const files = Array.from(e.target.files);

        files.forEach(file => {
            const reader = new FileReader();
            reader.onload = (event) => {
                const dataUrl = event.target.result;
                this.uploadedImages.push(dataUrl);
                this.displayUploadedImage(dataUrl);
            };
            reader.readAsDataURL(file);
        });

        // 重置input
        e.target.value = '';
    }

    displayUploadedImage(dataUrl) {
        const container = document.getElementById('uploadedImages');
        const imageDiv = document.createElement('div');
        imageDiv.className = 'uploaded-image';
        imageDiv.innerHTML = `
            <img src="${dataUrl}" alt="uploaded">
            <button class="remove-btn">×</button>
        `;

        imageDiv.querySelector('.remove-btn').addEventListener('click', () => {
            this.uploadedImages = this.uploadedImages.filter(img => img !== dataUrl);
            imageDiv.remove();
        });

        container.appendChild(imageDiv);
    }

    async deleteChat(chatId) {
        if (!confirm('确定要删除这个对话吗？')) return;

        try {
            const response = await fetch(`/api/chat/${chatId}`, {
                method: 'DELETE',
                headers: { 'Authorization': `Bearer ${this.token}` }
            });

            if (!response.ok) throw new Error('删除失败');

            if (this.currentChatId === chatId) {
                this.currentChatId = null;
                document.getElementById('chatMessages').innerHTML = `
                    <div class="welcome-message">
                        <h2>欢迎使用AI对话</h2>
                        <p>选择一个模型开始对话，或创建新的对话</p>
                    </div>
                `;
            }

            this.loadChats();
        } catch (error) {
            console.error('删除对话失败:', error);
            this.showError('删除对话失败');
        }
    }

    viewImage(url) {
        // 简单的图片查看，可以扩展为模态框
        window.open(url, '_blank');
    }

    showError(message) {
        alert(message);
    }
}

// 初始化
document.addEventListener('DOMContentLoaded', () => {
    new ChatManager();
});
