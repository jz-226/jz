// pages/index/index.js
const db = require('../../utils/db');
const util = require('../../utils/util');

Page({
  data: {
    // 统计数据
    stats: {
      studentsCount: 0,
      pendingQuestions: 0,
      pendingLessons: 0,
      weekTotal: 0,
      weekCompleted: 0
    },
    // 今日课程
    todayLessons: [],
    // 最近问题
    recentQuestions: [],
    // 今天日期
    today: '',
    todayDisplay: '',
    weekProgress: 0
  },

  onLoad() {
    this.setData({
      today: util.getToday(),
      todayDisplay: this.formatTodayDisplay()
    });
  },

  onShow() {
    // 设置自定义 tabBar 选中状态
    if (typeof this.getTabBar === 'function' && this.getTabBar()) {
      this.getTabBar().setData({ selected: 0 });
    }
    this.loadDashboard();
  },

  onPullDownRefresh() {
    this.loadDashboard().finally(() => wx.stopPullDownRefresh());
  },

  // 格式化今天日期显示
  formatTodayDisplay() {
    const d = new Date();
    const weekDays = ['日', '一', '二', '三', '四', '五', '六'];
    const month = d.getMonth() + 1;
    const day = d.getDate();
    const weekDay = weekDays[d.getDay()];
    return `${month}月${day}日 周${weekDay}`;
  },

  // 加载仪表盘数据
  async loadDashboard() {
    util.showLoading('加载中...');
    try {
      // 并行加载所有数据
      const [overview, todayLessons, recentQuestions, students, weekStats] = await Promise.all([
        db.stats.getOverview(),
        db.lessons.getToday(),
        db.questions.getAll(5),
        db.students.getAll(),
        db.stats.getWeekStats()
      ]);

      // 为今日课程添加学生姓名
      const todayLessonsWithStudent = todayLessons.map(lesson => {
        const student = students.find(s => s._id === lesson.studentId);
        return {
          ...lesson,
          studentName: student ? student.name : '未知'
        };
      });

      // 为最近问题添加学生姓名
      const questionsWithStudent = recentQuestions.map(q => {
        const student = students.find(s => s._id === q.studentId);
        return {
          ...q,
          studentName: student ? student.name : '未知'
        };
      });

      this.setData({
        stats: {
          studentsCount: overview.studentsCount,
          pendingQuestions: overview.pendingQuestions,
          pendingLessons: overview.pendingLessons,
          weekTotal: weekStats.weekTotal,
          weekCompleted: weekStats.weekCompleted
        },
        weekProgress: weekStats.weekTotal > 0
          ? Math.round(weekStats.weekCompleted / weekStats.weekTotal * 100)
          : 0,
        todayLessons: todayLessonsWithStudent,
        recentQuestions: questionsWithStudent
      });
    } catch (error) {
      console.error('加载仪表盘失败:', error);
      util.showError('加载失败，请下拉重试');
    } finally {
      util.hideLoading();
    }
  },

  // 跳转到学生列表
  goStudents() {
    util.switchTab('/pages/students/students');
  },

  // 跳转到课程列表
  goLessons() {
    util.switchTab('/pages/lessons/lessons');
  },

  // 跳转到错题列表
  goQuestions() {
    util.switchTab('/pages/questions/questions');
  },

  // 跳转到课程编辑
  goLessonEdit(e) {
    const lesson = e.currentTarget.dataset.lesson;
    util.navigateTo(`/pages/lesson-edit/lesson-edit?id=${lesson._id}`);
  },

  // 跳转到问题编辑
  goQuestionEdit(e) {
    const question = e.currentTarget.dataset.question;
    util.navigateTo(`/pages/question-edit/question-edit?id=${question._id}`);
  },

  // 查看全部今日课程
  viewAllTodayLessons() {
    util.navigateTo(`/pages/lessons/lessons?date=${this.data.today}`);
  },

  // 查看全部待讲解问题
  viewAllPendingQuestions() {
    util.switchTab('/pages/questions/questions');
  }
});
