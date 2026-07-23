// utils/db.js - 本地存储数据库操作封装
// 使用微信小程序本地存储，无需云开发环境即可使用

const STORAGE_KEYS = {
  students: 'tutor_students',
  lessons: 'tutor_lessons',
  questions: 'tutor_questions',
  summaries: 'tutor_summaries'
};

/**
 * 生成唯一ID
 */
function generateId() {
  return 'id_' + Date.now().toString(36) + '_' + Math.random().toString(36).slice(2, 11);
}

/**
 * 读取集合数据（带类型校验）
 */
function readCollection(key) {
  try {
    const data = wx.getStorageSync(key);
    if (!Array.isArray(data)) return [];
    return data;
  } catch (e) {
    console.warn('读取存储失败:', key, e);
    return [];
  }
}

/**
 * 写入集合数据
 */
function writeCollection(key, data) {
  try {
    wx.setStorageSync(key, data);
  } catch (e) {
    console.error('写入存储失败:', e);
    throw new Error('存储空间不足');
  }
}

/**
 * 模拟 serverDate
 */
function serverDate() {
  return new Date().toISOString();
}

/**
 * 学生相关操作
 */
const students = {
  // 获取所有学生
  getAll: async () => {
    const data = readCollection(STORAGE_KEYS.students);
    return data.sort((a, b) => new Date(b.createTime) - new Date(a.createTime));
  },

  // 根据ID获取学生
  getById: async (id) => {
    const data = readCollection(STORAGE_KEYS.students);
    const student = data.find(item => item._id === id);
    if (!student) throw new Error('学生不存在');
    return student;
  },

  // 添加学生
  add: async (data) => {
    const students = readCollection(STORAGE_KEYS.students);
    const now = serverDate();
    const newStudent = {
      _id: generateId(),
      ...data,
      createTime: now,
      updateTime: now
    };
    students.push(newStudent);
    writeCollection(STORAGE_KEYS.students, students);
    return newStudent._id;
  },

  // 更新学生
  update: async (id, data) => {
    const students = readCollection(STORAGE_KEYS.students);
    const index = students.findIndex(item => item._id === id);
    if (index === -1) throw new Error('学生不存在');
    data.updateTime = serverDate();
    students[index] = { ...students[index], ...data };
    writeCollection(STORAGE_KEYS.students, students);
  },

  // 删除学生
  delete: async (id) => {
    let students = readCollection(STORAGE_KEYS.students);
    students = students.filter(item => item._id !== id);
    writeCollection(STORAGE_KEYS.students, students);

    // 同时删除关联的课程、问题、总结
    let lessons = readCollection(STORAGE_KEYS.lessons);
    lessons = lessons.filter(item => item.studentId !== id);
    writeCollection(STORAGE_KEYS.lessons, lessons);

    let questions = readCollection(STORAGE_KEYS.questions);
    questions = questions.filter(item => item.studentId !== id);
    writeCollection(STORAGE_KEYS.questions, questions);

    let summaries = readCollection(STORAGE_KEYS.summaries);
    summaries = summaries.filter(item => item.studentId !== id);
    writeCollection(STORAGE_KEYS.summaries, summaries);
  },

  // 搜索学生
  search: async (keyword) => {
    const data = readCollection(STORAGE_KEYS.students);
    return data.filter(item => item.name && item.name.includes(keyword));
  }
};

/**
 * 课程相关操作
 */
const lessons = {
  // 获取所有课程
  getAll: async (limit = 20) => {
    const data = readCollection(STORAGE_KEYS.lessons);
    return data.sort((a, b) => new Date(b.date) - new Date(a.date)).slice(0, limit);
  },

  // 根据学生ID获取课程
  getByStudent: async (studentId) => {
    const data = readCollection(STORAGE_KEYS.lessons);
    return data
      .filter(item => item.studentId === studentId)
      .sort((a, b) => new Date(b.date) - new Date(a.date));
  },

  // 根据日期获取课程
  getByDate: async (dateStr) => {
    const data = readCollection(STORAGE_KEYS.lessons);
    return data
      .filter(item => item.date === dateStr)
      .sort((a, b) => new Date(b.createTime) - new Date(a.createTime));
  },

  // 获取今日课程
  getToday: async () => {
    const today = formatDate(new Date());
    return await lessons.getByDate(today);
  },

  // 根据状态获取课程
  getByStatus: async (status) => {
    const data = readCollection(STORAGE_KEYS.lessons);
    return data
      .filter(item => item.status === status)
      .sort((a, b) => new Date(b.date) - new Date(a.date));
  },

  // 根据ID获取课程
  getById: async (id) => {
    const data = readCollection(STORAGE_KEYS.lessons);
    const lesson = data.find(item => item._id === id);
    if (!lesson) throw new Error('课程不存在');
    return lesson;
  },

  // 添加课程
  add: async (data) => {
    const lessons = readCollection(STORAGE_KEYS.lessons);
    const now = serverDate();
    const newLesson = {
      _id: generateId(),
      ...data,
      createTime: now,
      updateTime: now
    };
    lessons.push(newLesson);
    writeCollection(STORAGE_KEYS.lessons, lessons);
    return newLesson._id;
  },

  // 更新课程
  update: async (id, data) => {
    const lessons = readCollection(STORAGE_KEYS.lessons);
    const index = lessons.findIndex(item => item._id === id);
    if (index === -1) throw new Error('课程不存在');
    data.updateTime = serverDate();
    lessons[index] = { ...lessons[index], ...data };
    writeCollection(STORAGE_KEYS.lessons, lessons);
  },

  // 删除课程
  delete: async (id) => {
    let lessons = readCollection(STORAGE_KEYS.lessons);
    lessons = lessons.filter(item => item._id !== id);
    writeCollection(STORAGE_KEYS.lessons, lessons);

    // 同时删除关联的总结
    let summaries = readCollection(STORAGE_KEYS.summaries);
    summaries = summaries.filter(item => item.lessonId !== id);
    writeCollection(STORAGE_KEYS.summaries, summaries);
  }
};

/**
 * 问题相关操作
 */
const questions = {
  // 获取所有问题
  getAll: async (limit = 20) => {
    const data = readCollection(STORAGE_KEYS.questions);
    return data.sort((a, b) => new Date(b.createTime) - new Date(a.createTime)).slice(0, limit);
  },

  // 根据学生ID获取问题
  getByStudent: async (studentId) => {
    const data = readCollection(STORAGE_KEYS.questions);
    return data
      .filter(item => item.studentId === studentId)
      .sort((a, b) => new Date(b.createTime) - new Date(a.createTime));
  },

  // 根据状态获取问题
  getByStatus: async (status) => {
    const data = readCollection(STORAGE_KEYS.questions);
    return data
      .filter(item => item.status === status)
      .sort((a, b) => new Date(b.createTime) - new Date(a.createTime));
  },

  // 根据科目获取问题
  getBySubject: async (subject) => {
    const data = readCollection(STORAGE_KEYS.questions);
    return data
      .filter(item => item.subject === subject)
      .sort((a, b) => new Date(b.createTime) - new Date(a.createTime));
  },

  // 根据ID获取问题
  getById: async (id) => {
    const data = readCollection(STORAGE_KEYS.questions);
    const question = data.find(item => item._id === id);
    if (!question) throw new Error('问题不存在');
    return question;
  },

  // 添加问题
  add: async (data) => {
    const questions = readCollection(STORAGE_KEYS.questions);
    const now = serverDate();
    const newQuestion = {
      _id: generateId(),
      ...data,
      createTime: now,
      updateTime: now
    };
    questions.push(newQuestion);
    writeCollection(STORAGE_KEYS.questions, questions);
    return newQuestion._id;
  },

  // 更新问题
  update: async (id, data) => {
    const questions = readCollection(STORAGE_KEYS.questions);
    const index = questions.findIndex(item => item._id === id);
    if (index === -1) throw new Error('问题不存在');
    data.updateTime = serverDate();
    questions[index] = { ...questions[index], ...data };
    writeCollection(STORAGE_KEYS.questions, questions);
  },

  // 删除问题
  delete: async (id) => {
    let questions = readCollection(STORAGE_KEYS.questions);
    questions = questions.filter(item => item._id !== id);
    writeCollection(STORAGE_KEYS.questions, questions);
  },

  // 获取待讲解问题数量
  countPending: async () => {
    const data = readCollection(STORAGE_KEYS.questions);
    return data.filter(item => item.status === 'pending').length;
  }
};

/**
 * 总结相关操作
 */
const summaries = {
  // 获取所有总结
  getAll: async (limit = 20) => {
    const data = readCollection(STORAGE_KEYS.summaries);
    return data.sort((a, b) => new Date(b.createTime) - new Date(a.createTime)).slice(0, limit);
  },

  // 根据学生ID获取总结
  getByStudent: async (studentId) => {
    const data = readCollection(STORAGE_KEYS.summaries);
    return data
      .filter(item => item.studentId === studentId)
      .sort((a, b) => new Date(b.createTime) - new Date(a.createTime));
  },

  // 根据课程ID获取总结
  getByLesson: async (lessonId) => {
    const data = readCollection(STORAGE_KEYS.summaries);
    const summary = data.find(item => item.lessonId === lessonId);
    return summary || null;
  },

  // 根据ID获取总结
  getById: async (id) => {
    const data = readCollection(STORAGE_KEYS.summaries);
    const summary = data.find(item => item._id === id);
    if (!summary) throw new Error('总结不存在');
    return summary;
  },

  // 添加总结
  add: async (data) => {
    const summaries = readCollection(STORAGE_KEYS.summaries);
    const now = serverDate();
    const newSummary = {
      _id: generateId(),
      ...data,
      createTime: now,
      updateTime: now
    };
    summaries.push(newSummary);
    writeCollection(STORAGE_KEYS.summaries, summaries);
    return newSummary._id;
  },

  // 更新总结
  update: async (id, data) => {
    const summaries = readCollection(STORAGE_KEYS.summaries);
    const index = summaries.findIndex(item => item._id === id);
    if (index === -1) throw new Error('总结不存在');
    data.updateTime = serverDate();
    summaries[index] = { ...summaries[index], ...data };
    writeCollection(STORAGE_KEYS.summaries, summaries);
  },

  // 删除总结
  delete: async (id) => {
    let summaries = readCollection(STORAGE_KEYS.summaries);
    summaries = summaries.filter(item => item._id !== id);
    writeCollection(STORAGE_KEYS.summaries, summaries);
  }
};

/**
 * 统计相关操作
 */
const stats = {
  // 获取首页统计数据
  getOverview: async () => {
    const studentsData = readCollection(STORAGE_KEYS.students);
    const lessonsData = readCollection(STORAGE_KEYS.lessons);
    const questionsData = readCollection(STORAGE_KEYS.questions);

    const pendingQuestionsCount = questionsData.filter(q => q.status === 'pending').length;
    const pendingLessonsCount = lessonsData.filter(l => l.status === 'pending').length;
    const recentQuestions = questionsData
      .sort((a, b) => new Date(b.createTime) - new Date(a.createTime))
      .slice(0, 5);

    return {
      studentsCount: studentsData.length,
      pendingQuestions: pendingQuestionsCount,
      pendingLessons: pendingLessonsCount,
      recentQuestions: recentQuestions
    };
  },

  // 获取学生统计
  getStudentStats: async (studentId) => {
    const questionsData = readCollection(STORAGE_KEYS.questions);
    const lessonsData = readCollection(STORAGE_KEYS.lessons);
    const summariesData = readCollection(STORAGE_KEYS.summaries);

    return {
      questionsCount: questionsData.filter(q => q.studentId === studentId).length,
      lessonsCount: lessonsData.filter(l => l.studentId === studentId).length,
      summariesCount: summariesData.filter(s => s.studentId === studentId).length
    };
  },

  // 获取本周课程统计（新增）
  getWeekStats: async () => {
    const lessonsData = readCollection(STORAGE_KEYS.lessons);
    const today = new Date();
    const dayOfWeek = today.getDay();
    const monday = new Date(today);
    monday.setDate(today.getDate() - (dayOfWeek === 0 ? 6 : dayOfWeek - 1));
    const mondayStr = formatDate(monday);

    const weekLessons = lessonsData.filter(l => l.date >= mondayStr);
    const completedCount = weekLessons.filter(l => l.status === 'completed').length;

    return {
      weekTotal: weekLessons.length,
      weekCompleted: completedCount,
      weekPending: weekLessons.length - completedCount
    };
  }
};

// 格式化日期
function formatDate(date) {
  const d = new Date(date);
  const year = d.getFullYear();
  const month = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

// 格式化时间
function formatTime(date) {
  const d = new Date(date);
  const hours = String(d.getHours()).padStart(2, '0');
  const minutes = String(d.getMinutes()).padStart(2, '0');
  return `${hours}:${minutes}`;
}

// 格式化完整时间
function formatFullTime(date) {
  return `${formatDate(date)} ${formatTime(date)}`;
}

module.exports = {
  students,
  lessons,
  questions,
  summaries,
  stats,
  readCollection,
  STORAGE_KEYS,
  formatDate,
  formatTime,
  formatFullTime
};
