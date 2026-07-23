// components/lesson-card/lesson-card.js
Component({
  properties: {
    lesson: {
      type: Object,
      value: {}
    },
    studentName: {
      type: String,
      value: ''
    }
  },

  data: {
    day: '',
    weekDay: '',
    statusText: ''
  },

  observers: {
    'lesson': function(lesson) {
      try {
        if (lesson && lesson.date) {
          const date = new Date(lesson.date);
          const weekDays = ['周日', '周一', '周二', '周三', '周四', '周五', '周六'];

          this.setData({
            day: date.getDate(),
            weekDay: weekDays[date.getDay()] || '',
            statusText: lesson.status === 'completed' ? '已完成' : '待上课'
          });
        }
      } catch (e) {
        console.warn('lesson-card 组件数据更新异常:', e);
      }
    }
  },

  methods: {
    onTap() {
      this.triggerEvent('tap', { lesson: this.properties.lesson });
    }
  }
});
