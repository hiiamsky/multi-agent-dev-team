import { createRouter, createWebHashHistory } from 'vue-router'
import NumPadPage from './pages/NumPadPage.vue'
import TodayMenuPage from './pages/TodayMenuPage.vue'

const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    {
      path: '/',
      redirect: '/menu'
    },
    {
      path: '/numpad',
      name: 'NumPad',
      component: NumPadPage
    },
    {
      path: '/menu',
      name: 'TodayMenu',
      component: TodayMenuPage
    }
  ]
})

export default router