import RollbackPage from './views/RollbackPage.vue';
import './style.css';

export const pluginConfig = {
    name: 'RollbackPlugin',
    version: '1.0.0',

    routes: [
        {
            parentName: 'instance',
            path: 'rollback',
            name: 'InstanceRollback',
            component: RollbackPage,
            meta: { title: '服务器回档', icon: 'backup', roleCode: ['admin'] },
        }
    ]
};