<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { MessagePlugin, NotifyPlugin } from 'tdesign-vue-next';
import { BackupIcon, ServerIcon, LockOnIcon, ErrorTriangleIcon, FileTxtIcon, PlayCircleIcon, StopCircleIcon, RefreshIcon, TimeIcon, SendIcon } from 'tdesign-icons-vue-next';

interface BackupInfo {
  fileName: string;
  filePath: string;
  createTime: string;
  size: number;
}

interface ServerInfo {
  serverId: string;
  serverName: string;
  serverPath: string;
  worldPath: string;
  backupPath: string;
  isRunning: boolean;
  playerCount: number;
}

interface RollbackLog {
  Time: string;
  BackupFileName: string;
  TargetPath: string;
  Status: string;
}

const backups = ref<BackupInfo[]>([]);
const selectedBackup = ref<BackupInfo | null>(null);
const serverInfo = ref<ServerInfo | null>(null);
const customWorldPath = ref('');
const showConfirmDialog = ref(false);
const countdown = ref(30);
const countdownTimer = ref<number | null>(null);
const isExecuting = ref(false);
const showLogs = ref(false);
const logs = ref<RollbackLog[]>([]);
const announcement = ref('服务器即将进行回档维护，请玩家做好准备！');
const sendAnnouncement = ref(true);
const loadingBackups = ref(false);
const loadingServerInfo = ref(false);

const formatFileSize = (bytes: number): string => {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
};

const formatDateTime = (dateStr: string): string => {
  const date = new Date(dateStr);
  return date.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  });
};

const getAuthToken = () => {
  const token = localStorage.getItem('mslx-web-token');
  
  console.log('=== getAuthToken ===');
  console.log('token from localStorage.mslx-web-token:', token ? 'found' : 'not found');
  
  return token;
};

const loadBackups = async () => {
  loadingBackups.value = true;
  try {
    console.log('loadBackups: starting...');
    
    const apiUrl = '/api/plugins/mslx-plugin-demo/rollback/backups';
    console.log('loadBackups: API URL:', apiUrl);
    
    const token = getAuthToken();
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      'Accept': 'application/json'
    };
    if (token) {
      headers['x-user-token'] = token;
      console.log('loadBackups: added x-user-token header');
    }
    
    const response = await fetch(apiUrl, {
      method: 'GET',
      headers: headers,
      credentials: 'include'
    });
    
    console.log('loadBackups: response status:', response.status);
    console.log('loadBackups: response ok:', response.ok);
    console.log('loadBackups: response headers:', response.headers);
    
    const text = await response.text();
    console.log('loadBackups: response text length:', text.length);
    console.log('loadBackups: response text:', text.length > 500 ? text.substring(0, 500) + '...' : text);
    
    if (!text) {
      console.error('loadBackups: empty response');
      MessagePlugin.error('获取备份失败: 空响应');
      return;
    }
    
    let res;
    try {
      res = JSON.parse(text);
      console.log('loadBackups: parsed JSON:', res);
    } catch (parseError) {
      console.error('loadBackups: JSON parse error:', parseError);
      console.error('loadBackups: raw response:', text);
      MessagePlugin.error('获取备份失败: 响应格式错误');
      return;
    }
    
    if (res.code === 200) {
      backups.value = res.data || [];
      if (backups.value.length > 0) {
        selectedBackup.value = backups.value[0];
      }
      console.log('loadBackups: success, got', backups.value.length, 'backups');
    } else {
      console.log('loadBackups: error response:', res.message);
      MessagePlugin.warning(res.message || '获取备份失败');
    }
  } catch (err: any) {
    console.error('loadBackups error:', err);
    console.error('loadBackups error stack:', err.stack);
    MessagePlugin.error('加载备份列表失败: ' + (err.message || err));
  } finally {
    loadingBackups.value = false;
  }
};

const loadServerInfo = async () => {
  loadingServerInfo.value = true;
  try {
    console.log('loadServerInfo: starting...');
    
    const apiUrl = '/api/plugins/mslx-plugin-demo/rollback/server-info';
    console.log('loadServerInfo: API URL:', apiUrl);
    
    const token = getAuthToken();
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      'Accept': 'application/json'
    };
    if (token) {
      headers['x-user-token'] = token;
      console.log('loadServerInfo: added x-user-token header');
    }
    
    const response = await fetch(apiUrl, {
      method: 'GET',
      headers: headers,
      credentials: 'include'
    });
    
    console.log('loadServerInfo: response status:', response.status);
    console.log('loadServerInfo: response ok:', response.ok);
    console.log('loadServerInfo: response headers:', response.headers);
    
    const text = await response.text();
    console.log('loadServerInfo: response text length:', text.length);
    console.log('loadServerInfo: response text:', text.length > 500 ? text.substring(0, 500) + '...' : text);
    
    if (!text) {
      console.error('loadServerInfo: empty response');
      MessagePlugin.error('获取服务器信息失败: 空响应');
      return;
    }
    
    let res;
    try {
      res = JSON.parse(text);
      console.log('loadServerInfo: parsed JSON:', res);
    } catch (parseError) {
      console.error('loadServerInfo: JSON parse error:', parseError);
      console.error('loadServerInfo: raw response:', text);
      MessagePlugin.error('获取服务器信息失败: 响应格式错误');
      return;
    }
    
    if (res.code === 200) {
      if (res.data) {
        serverInfo.value = res.data;
        if (res.data.worldPath) {
          customWorldPath.value = res.data.worldPath;
        }
        console.log('loadServerInfo: success, serverInfo:', serverInfo.value);
      }
    } else {
      console.log('loadServerInfo: error response:', res.message);
      MessagePlugin.warning(res.message || '获取服务器信息失败');
    }
  } catch (err: any) {
    console.error('loadServerInfo error:', err);
    console.error('loadServerInfo error stack:', err.stack);
    MessagePlugin.error('加载服务器信息失败: ' + (err.message || err));
  } finally {
    loadingServerInfo.value = false;
  }
};

const loadLogs = async () => {
  try {
    console.log('loadLogs: starting...');
    
    const token = getAuthToken();
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      'Accept': 'application/json'
    };
    if (token) {
      headers['x-user-token'] = token;
    }
    
    const response = await fetch('/api/plugins/mslx-plugin-demo/rollback/logs', {
      method: 'GET',
      headers: headers,
      credentials: 'include'
    });
    
    const text = await response.text();
    console.log('loadLogs: response text:', text);
    
    const res = JSON.parse(text);
    console.log('loadLogs: parsed JSON:', res);
    
    if (res.code === 200) {
      logs.value = res.data || [];
    }
  } catch (err: any) {
    console.error('loadLogs error:', err);
    MessagePlugin.error('加载日志失败: ' + (err.message || err));
  }
};

const openConfirmDialog = () => {
  if (!selectedBackup.value) {
    MessagePlugin.warning('请先选择一个备份文件');
    return;
  }
  if (!customWorldPath.value) {
    MessagePlugin.warning('请设置存档路径');
    return;
  }
  showConfirmDialog.value = true;
  countdown.value = 30;
  startCountdown();
};

const startCountdown = () => {
  if (countdownTimer.value) {
    clearInterval(countdownTimer.value);
  }
  countdownTimer.value = window.setInterval(() => {
    if (countdown.value > 0) {
      countdown.value--;
    } else {
      executeRollback();
    }
  }, 1000);
};

const cancelCountdown = () => {
  if (countdownTimer.value) {
    clearInterval(countdownTimer.value);
    countdownTimer.value = null;
  }
  showConfirmDialog.value = false;
};

const executeRollback = async () => {
  if (!selectedBackup.value || !customWorldPath.value) return;
  cancelCountdown();
  isExecuting.value = true;
  try {
    console.log('executeRollback: starting...');
    
    const token = getAuthToken();
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      'Accept': 'application/json'
    };
    if (token) {
      headers['x-user-token'] = token;
    }
    
    const response = await fetch('/api/plugins/mslx-plugin-demo/rollback/execute', {
      method: 'POST',
      headers: headers,
      credentials: 'include',
      body: JSON.stringify({
        BackupPath: selectedBackup.value.filePath,
        WorldPath: customWorldPath.value,
        Announcement: sendAnnouncement.value ? announcement.value : ''
      })
    });
    
    const text = await response.text();
    console.log('executeRollback: response text:', text);
    
    const res = JSON.parse(text);
    console.log('executeRollback: parsed JSON:', res);
    
    if (res.code === 200) {
      NotifyPlugin.success({
        title: '回档成功',
        content: res.data || '回档操作已完成',
        duration: 5000
      });
    } else {
      MessagePlugin.error(res.message || '回档失败');
    }
  } catch (err: any) {
    console.error('executeRollback error:', err);
    MessagePlugin.error('回档失败: ' + (err.message || err));
  } finally {
    isExecuting.value = false;
    showConfirmDialog.value = false;
    await loadServerInfo();
    await loadLogs();
  }
};

const refreshData = () => {
  loadBackups();
  loadServerInfo();
};

const statusColor = computed(() => {
  if (!serverInfo.value) return 'default';
  return serverInfo.value.isRunning ? 'success' : 'danger';
});

onMounted(() => {
  loadBackups();
  loadServerInfo();
});
</script>

<template>
  <div class="mx-auto flex flex-col gap-6 pb-8 pt-6">
    <div class="design-card rounded-2xl glass-card border border-[var(--td-component-border)] shadow-sm p-6">
      <div class="flex items-center justify-between mb-6">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-primary-light flex items-center justify-center">
            <BackupIcon class="text-[var(--color-primary)]" size="24px" />
          </div>
          <div>
            <h1 class="text-xl font-bold m-0">服务器回档工具</h1>
            <p class="text-sm text-[var(--td-text-color-secondary)] m-0">一键恢复服务器存档到指定备份版本</p>
          </div>
        </div>
        <t-button variant="outline" @click="refreshData" :loading="loadingBackups || loadingServerInfo">
          <template #icon><RefreshIcon /></template>
          刷新
        </t-button>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div class="lg:col-span-2 space-y-6">
          <div class="bg-secondary-light rounded-xl p-5 border border-[var(--td-component-border)]">
            <div class="flex items-center gap-2 mb-4">
              <ServerIcon class="text-[var(--color-primary)]" size="18px" />
              <h3 class="font-bold">服务器状态</h3>
            </div>
            
            <div v-if="loadingServerInfo" class="flex items-center justify-center py-8">
              <t-loading size="medium" />
            </div>
            
            <div v-else-if="serverInfo" class="space-y-4">
              <div class="flex items-center justify-between">
                <span class="text-[var(--td-text-color-secondary)]">服务器名称</span>
                <span class="font-medium">{{ serverInfo.serverName || '未知' }}</span>
              </div>
              <div class="flex items-center justify-between">
                <span class="text-[var(--td-text-color-secondary)]">运行状态</span>
                <t-tag :theme="statusColor" :size="'small'">
                  <template #icon>
                    <PlayCircleIcon v-if="serverInfo.isRunning" size="14px" />
                    <StopCircleIcon v-else size="14px" />
                  </template>
                  {{ serverInfo.isRunning ? '运行中' : '已停止' }}
                </t-tag>
              </div>
              <div class="flex items-center justify-between">
                <span class="text-[var(--td-text-color-secondary)]">在线人数</span>
                <span class="font-medium">{{ serverInfo.playerCount || 0 }} 人</span>
              </div>
              <div class="flex items-center justify-between">
                <span class="text-[var(--td-text-color-secondary)]">服务器路径</span>
                <span class="font-medium text-xs text-[var(--td-text-color-primary)]">{{ serverInfo.serverPath || '未知' }}</span>
              </div>
              <div class="flex items-center justify-between">
                <span class="text-[var(--td-text-color-secondary)]">备份路径</span>
                <span class="font-medium text-xs text-[var(--td-text-color-primary)]">{{ serverInfo.backupPath || '未知' }}</span>
              </div>
            </div>
            
            <div v-else class="text-center py-8 text-[var(--td-text-color-secondary)]">
              暂无服务器信息
            </div>
          </div>

          <div class="bg-secondary-light rounded-xl p-5 border border-[var(--td-component-border)]">
            <div class="flex items-center gap-2 mb-4">
              <FileTxtIcon class="text-[var(--color-primary)]" size="18px" />
              <h3 class="font-bold">存档路径设置</h3>
            </div>
            
            <t-input
              v-model="customWorldPath"
              placeholder="请输入存档路径，例如 /path/to/server/world"
              class="w-full"
              :disabled="isExecuting"
            >
              <template #suffix>
                <span class="text-xs text-[var(--td-text-color-secondary)] ml-2">
                  {{ customWorldPath ? '路径已设置' : '未设置' }}
                </span>
              </template>
            </t-input>
            
            <div v-if="serverInfo?.worldPath" class="mt-3">
              <t-button size="small" variant="outline" @click="customWorldPath = serverInfo.worldPath">
                使用默认路径
              </t-button>
              <span class="ml-3 text-xs text-[var(--td-text-color-secondary)]">
                默认: {{ serverInfo.worldPath }}
              </span>
            </div>
          </div>

          <div class="bg-secondary-light rounded-xl p-5 border border-[var(--td-component-border)]">
            <div class="flex items-center justify-between mb-4">
              <div class="flex items-center gap-2">
                <BackupIcon class="text-[var(--color-primary)]" size="18px" />
                <h3 class="font-bold">选择备份文件</h3>
              </div>
              <span class="text-xs text-[var(--td-text-color-secondary)]">共 {{ backups.length }} 个备份</span>
            </div>
            
            <div v-if="loadingBackups" class="flex items-center justify-center py-8">
              <t-loading size="medium" />
            </div>
            
            <div v-else-if="backups.length === 0" class="text-center py-8 text-[var(--td-text-color-secondary)]">
              <BackupIcon size="48px" class="mx-auto mb-3 opacity-50" />
              <p>暂无备份文件</p>
            </div>
            
            <div v-else class="space-y-2 max-h-[300px] overflow-y-auto custom-scrollbar">
              <t-radio-group v-model="selectedBackup">
                <t-radio
                  v-for="backup in backups"
                  :key="backup.filePath"
                  :value="backup"
                  class="w-full"
                  :disabled="isExecuting"
                >
                  <div class="flex items-center justify-between w-full py-3 px-4 bg-[var(--td-bg-color-container)] rounded-lg">
                    <div class="flex items-center gap-3">
                      <div class="w-8 h-8 rounded-lg bg-primary-light flex items-center justify-center">
                        <FileTxtIcon size="16px" class="text-[var(--color-primary)]" />
                      </div>
                      <div>
                        <div class="font-medium text-sm">{{ backup.fileName }}</div>
                        <div class="text-xs text-[var(--td-text-color-secondary)]">
                          {{ formatDateTime(backup.createTime) }}
                        </div>
                      </div>
                    </div>
                    <div class="text-sm text-[var(--td-text-color-secondary)]">
                      {{ formatFileSize(backup.size) }}
                    </div>
                  </div>
                </t-radio>
              </t-radio-group>
            </div>
          </div>
        </div>

        <div class="space-y-6">
          <div class="bg-secondary-light rounded-xl p-5 border border-[var(--td-component-border)]">
            <div class="flex items-center gap-2 mb-4">
              <SendIcon class="text-[var(--color-primary)]" size="18px" />
              <h3 class="font-bold">回档公告</h3>
            </div>
            
            <t-switch
              v-model="sendAnnouncement"
              label="发送公告"
              :disabled="isExecuting"
            />
            
            <t-textarea
              v-model="announcement"
              placeholder="输入要发送的公告内容"
              :rows="4"
              :disabled="!sendAnnouncement || isExecuting"
              class="mt-3"
            />
            
            <div v-if="serverInfo?.isRunning && sendAnnouncement" class="mt-3 p-3 bg-warning-light rounded-lg">
              <p class="text-xs text-[var(--color-warning)] m-0 flex items-center gap-2">
                <ErrorTriangleIcon size="14px" />
                公告将发送给当前所有在线玩家
              </p>
            </div>
          </div>

          <div class="bg-secondary-light rounded-xl p-5 border border-[var(--td-component-border)]">
            <div class="flex items-center gap-2 mb-4">
              <LockOnIcon class="text-[var(--color-primary)]" size="18px" />
              <h3 class="font-bold">倒计时设置</h3>
            </div>
            
            <div class="flex items-center gap-3">
              <span class="text-sm">确认倒计时</span>
              <t-input-number
                v-model="countdown"
                :min="5"
                :max="120"
                :disabled="showConfirmDialog || isExecuting"
                class="w-24"
              />
              <span class="text-sm">秒</span>
            </div>
            
            <p class="text-xs text-[var(--td-text-color-secondary)] mt-3">
              点击回档后将开始倒计时，倒计时结束后自动执行回档操作
            </p>
          </div>

          <div class="bg-primary-light rounded-xl p-5 border border-[var(--color-primary)]">
            <div class="flex items-center gap-2 mb-3">
              <ErrorTriangleIcon class="text-[var(--color-danger)]" size="18px" />
              <h3 class="font-bold text-[var(--color-danger)]">操作警告</h3>
            </div>
            <ul class="text-xs text-[var(--td-text-color-secondary)] space-y-2">
              <li>• 回档操作将覆盖当前存档</li>
              <li>• 回档前服务器将自动停止</li>
              <li>• 回档完成后服务器将自动重启</li>
              <li>• 请确保已备份重要数据</li>
            </ul>
            
            <t-button
              theme="danger"
              class="w-full mt-4"
              :loading="isExecuting"
              :disabled="!selectedBackup || !customWorldPath || isExecuting"
              @click="openConfirmDialog"
            >
              <template #icon><BackupIcon /></template>
              执行回档
            </t-button>
          </div>

          <t-button
            variant="outline"
            class="w-full"
            @click="showLogs = true"
          >
            <template #icon><FileTxtIcon /></template>
            查看回档日志
          </t-button>
        </div>
      </div>
    </div>

    <t-dialog
      v-model="showConfirmDialog"
      header="确认回档"
      :closable="false"
      :footer="false"
      width="500px"
    >
      <div class="text-center py-8">
        <div class="w-16 h-16 rounded-full bg-danger-light flex items-center justify-center mx-auto mb-4">
          <ErrorTriangleIcon size="32px" class="text-[var(--color-danger)]" />
        </div>
        <h3 class="text-lg font-bold mb-2">即将执行回档操作</h3>
        <p class="text-[var(--td-text-color-secondary)] mb-6">
          将使用 <span class="font-medium">{{ selectedBackup?.fileName }}</span> 恢复存档
        </p>
        
        <div class="flex items-center justify-center gap-3 mb-6">
          <TimeIcon class="text-[var(--color-warning)]" size="24px" />
          <span class="text-4xl font-bold text-[var(--color-warning)]">{{ countdown }}</span>
          <span class="text-[var(--td-text-color-secondary)]">秒后自动执行</span>
        </div>
        
        <div class="flex gap-3 justify-center">
          <t-button variant="outline" @click="cancelCountdown">取消</t-button>
          <t-button theme="danger" @click="executeRollback">立即执行</t-button>
        </div>
      </div>
    </t-dialog>

    <t-dialog
      v-model="showLogs"
      header="回档日志"
      width="800px"
    >
      <div class="max-h-[500px] overflow-y-auto custom-scrollbar">
        <div v-if="logs.length === 0" class="text-center py-8 text-[var(--td-text-color-secondary)]">
          <FileTxtIcon size="48px" class="mx-auto mb-3 opacity-50" />
          <p>暂无回档日志</p>
        </div>
        
        <div v-else class="space-y-4">
          <div
            v-for="(log, index) in logs"
            :key="index"
            class="p-4 bg-secondary-light rounded-lg border border-[var(--td-component-border)]"
          >
            <div class="flex items-center justify-between mb-2">
              <span class="font-medium">{{ log.BackupFileName }}</span>
              <t-tag :theme="log.Status === 'success' ? 'success' : 'danger'" size="small">
                {{ log.Status === 'success' ? '成功' : '失败' }}
              </t-tag>
            </div>
            <div class="text-sm text-[var(--td-text-color-secondary)] mb-1">
              目标路径: {{ log.TargetPath }}
            </div>
            <div class="text-xs text-[var(--td-text-color-secondary)]">
              操作时间: {{ formatDateTime(log.Time) }}
            </div>
          </div>
        </div>
      </div>
    </t-dialog>
  </div>
</template>

<style scoped>
@unocss;
.bg-primary-light { background-color: color-mix(in srgb, var(--td-brand-color) 10%, transparent); }
.bg-success-light { background-color: color-mix(in srgb, var(--td-success-color) 10%, transparent); }
.bg-danger-light { background-color: color-mix(in srgb, var(--td-danger-color) 10%, transparent); }
.bg-warning-light { background-color: color-mix(in srgb, var(--td-warning-color) 10%, transparent); }
.bg-secondary-light { background-color: color-mix(in srgb, var(--td-bg-color-secondarycontainer) 50%, transparent); }
.custom-scrollbar::-webkit-scrollbar { width: 8px; height: 8px; }
.custom-scrollbar::-webkit-scrollbar-track { background: var(--td-bg-color-secondary); border-radius: 4px; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: var(--td-brand-color); border-radius: 4px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: var(--td-brand-color-hover); }
</style>
