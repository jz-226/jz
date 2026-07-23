// components/student-card/student-card.js
Component({
  properties: {
    student: {
      type: Object,
      value: {}
    },
    showStats: {
      type: Boolean,
      value: false
    },
    stats: {
      type: Object,
      value: {}
    }
  },

  data: {
    avatarColor: '',
    initial: '',
    subjectsText: ''
  },

  observers: {
    'student': function(student) {
      try {
        if (student && student.name) {
          this.setData({
            avatarColor: this.getAvatarColor(student.name),
            initial: student.name.charAt(0),
            subjectsText: (student.subjects || []).join(' · ')
          });
        }
      } catch (e) {
        console.warn('student-card 组件数据更新异常:', e);
      }
    }
  },

  methods: {
    getAvatarColor(name) {
      try {
        const colors = [
          '#4A90D9', '#5C6BC0', '#7E57C2', '#AB47BC',
          '#E91E63', '#EC407A', '#F44336', '#EF5350',
          '#FF7043', '#FF9800', '#FFCA28', '#66BB6A'
        ];
        const index = (name || '?').charCodeAt(0) % colors.length;
        return colors[index];
      } catch (e) {
        return '#4A90D9';
      }
    },

    onTap() {
      this.triggerEvent('tap', { student: this.properties.student });
    }
  }
});
