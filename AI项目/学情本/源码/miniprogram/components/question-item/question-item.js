// components/question-item/question-item.js
const util = require('../../utils/util');
const db = require('../../utils/db');

Component({
  properties: {
    question: {
      type: Object,
      value: {}
    },
    studentName: {
      type: String,
      value: ''
    },
    showActions: {
      type: Boolean,
      value: false
    }
  },

  data: {
    statusText: '',
    timeText: ''
  },

  observers: {
    'question': function(question) {
      try {
        if (question) {
          const statusMap = {
            'pending': '待讲解',
            'explained': '已讲解',
            'mastered': '已掌握'
          };

          this.setData({
            statusText: statusMap[question.status] || '待讲解',
            timeText: util.formatDateFriendly(util.formatDate(question.createTime))
          });
        }
      } catch (e) {
        console.warn('question-item 组件数据更新异常:', e);
      }
    }
  },

  methods: {
    onTap() {
      this.triggerEvent('tap', { question: this.properties.question });
    },

    async onStatusChange(e) {
      const status = e.currentTarget.dataset.status;
      const question = this.properties.question;

      try {
        await db.questions.update(question._id, { status });
        this.setData({
          statusText: {
            'pending': '待讲解',
            'explained': '已讲解',
            'mastered': '已掌握'
          }[status]
        });
        util.showSuccess('状态已更新');
        this.triggerEvent('statusChange', { question, status });
      } catch (error) {
        console.error('更新状态失败:', error);
        util.showError('更新失败');
      }
    }
  }
});
